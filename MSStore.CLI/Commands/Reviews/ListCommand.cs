// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using MSStore.CLI.Services.Translation;
using Spectre.Console;

namespace MSStore.CLI.Commands.Reviews
{
    internal class ListCommand : Command
    {
        internal static readonly Option<int?> TopOption;
        internal static readonly Option<int?> SkipOption;
        internal static readonly Option<int?> RatingOption;
        internal static readonly Option<string> MarketOption;

        private const int MaxTextLengthInTable = 120;

        static ListCommand()
        {
            TopOption = new Option<int?>("--top", "-t")
            {
                Description = $"The maximum number of reviews to return. The Microsoft Store accepts at most {StorePackagedAPI.MaxReviewsPerRequest}."
            };

            SkipOption = new Option<int?>("--skip", "-s")
            {
                Description = "The number of reviews to skip, for paging through large result sets."
            };

            RatingOption = new Option<int?>("--rating")
            {
                Description = "Only return reviews with this star rating (1-5)."
            };

            MarketOption = new Option<string>("--market", "-m")
            {
                Description = "Only return reviews from this market, as an ISO 3166 country code (for example 'US')."
            };
        }

        public ListCommand()
            : base("list", "List the reviews of an application.")
        {
            Arguments.Add(ReviewsCommand.ProductIdArgument);
            Options.Add(ReviewsCommand.StartDateOption);
            Options.Add(ReviewsCommand.EndDateOption);
            Options.Add(TopOption);
            Options.Add(SkipOption);
            Options.Add(RatingOption);
            Options.Add(MarketOption);
            Options.Add(ReviewsCommand.TranslateOption);
        }

        public class Handler(
            ILogger<ListCommand.Handler> logger,
            IStoreAPIFactory storeAPIFactory,
            ITranslationService translationService,
            IAnsiConsole ansiConsole,
            TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly ITranslationService _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                string productId = parseResult.GetRequiredValue(ReviewsCommand.ProductIdArgument);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var top = parseResult.GetValue(TopOption);
                if (top is > StorePackagedAPI.MaxReviewsPerRequest)
                {
                    _ansiConsole.MarkupLine($"[bold red]--top cannot be greater than {StorePackagedAPI.MaxReviewsPerRequest}.[/]");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var rating = parseResult.GetValue(RatingOption);
                if (rating is < 1 or > 5)
                {
                    _ansiConsole.MarkupLine("[bold red]--rating must be between 1 and 5.[/]");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var translateLanguage = parseResult.GetTranslateLanguage();

                var reviews = await _ansiConsole.Status().StartAsync("Retrieving Reviews", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var response = await storePackagedAPI.GetAppReviewsAsync(
                            productId,
                            parseResult.GetValue(ReviewsCommand.StartDateOption),
                            parseResult.GetValue(ReviewsCommand.EndDateOption),
                            top,
                            parseResult.GetValue(SkipOption),
                            BuildFilter(rating, parseResult.GetValue(MarketOption)),
                            "date desc",
                            ct);

                        var reviews = response.Value ?? [];

                        if (translateLanguage != null && reviews.Count > 0)
                        {
                            ctx.Status("Translating Reviews");
                            await ReviewTranslator.TranslateAsync(_translationService, reviews, translateLanguage, ct);
                        }

                        ctx.SuccessStatus(_ansiConsole, "[bold green]Retrieved Reviews[/]");

                        return reviews;
                    }
                    catch (TranslationException err)
                    {
                        _logger.LogError(err, "Error while translating Reviews.");
                        ctx.ErrorStatus(_ansiConsole, err.Message);
                        return null;
                    }
                    catch (MSStoreHttpException err)
                    {
                        _logger.LogError(err, "Error while retrieving Reviews.");

                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not find the Application. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while retrieving Reviews.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving Reviews.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (reviews == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                if (reviews.Count == 0)
                {
                    // Only refer to a period or filters when the caller actually narrowed the
                    // query. With no options the service returns reviews from every date, so
                    // implying a range was applied would be misleading.
                    var narrowedByDate = parseResult.NarrowedReviewsByDate();
                    var narrowedByFilter = rating.HasValue || !string.IsNullOrWhiteSpace(parseResult.GetValue(MarketOption));

                    _ansiConsole.MarkupLine((narrowedByDate, narrowedByFilter) switch
                    {
                        (true, true) => "This application has [bold][u]no[/] reviews[/] matching the requested period and filters.",
                        (true, false) => "This application has [bold][u]no[/] reviews[/] for the requested period.",
                        (false, true) => "This application has [bold][u]no[/] reviews[/] matching the requested filters.",
                        (false, false) => "This application has [bold][u]no[/] reviews[/]."
                    });

                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
                }

                _ansiConsole.Write(BuildTable(reviews, translateLanguage));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
            }

            /// <summary>
            /// Builds the OData-style filter accepted by the analytics API. String values are
            /// single-quoted, and single quotes inside them are doubled.
            /// </summary>
            private static string? BuildFilter(int? rating, string? market)
            {
                var clauses = new List<string>();

                if (rating.HasValue)
                {
                    clauses.Add($"rating eq {rating.Value.ToString(CultureInfo.InvariantCulture)}");
                }

                if (!string.IsNullOrWhiteSpace(market))
                {
                    clauses.Add($"market eq '{market.Replace("'", "''", StringComparison.Ordinal)}'");
                }

                return clauses.Count == 0 ? null : string.Join(" and ", clauses);
            }

            private static Table BuildTable(IReadOnlyList<AppReview> reviews, string? translateLanguage)
            {
                var translated = translateLanguage != null;

                // The Id is shown rather than a running index because it is the value
                // 'reviews get' takes, and there is no other way to discover it.
                var table = new Table();
                if (translated)
                {
                    table.AddColumns("Id", "Date", "Rating", "Market", "Lang", "Reviewer", "Title", "Review", "Reply");
                }
                else
                {
                    table.AddColumns("Id", "Date", "Rating", "Market", "Reviewer", "Title", "Review", "Reply");
                }

                foreach (var review in reviews)
                {
                    var title = (translated ? review.TranslatedReviewTitle ?? review.ReviewTitle : review.ReviewTitle) ?? string.Empty;
                    var text = (translated ? review.TranslatedReviewText ?? review.ReviewText : review.ReviewText) ?? string.Empty;

                    var cells = new List<string>
                    {
                        (review.Id ?? string.Empty).EscapeMarkup(),
                        (review.Date ?? string.Empty).EscapeMarkup(),
                        FormatRating(review.Rating),
                        (review.Market ?? string.Empty).EscapeMarkup(),
                    };

                    if (translated)
                    {
                        cells.Add((review.DetectedLanguage ?? string.Empty).EscapeMarkup());
                    }

                    cells.Add((review.ReviewerName ?? string.Empty).EscapeMarkup());
                    cells.Add(Truncate(title).EscapeMarkup());
                    cells.Add(Truncate(text).EscapeMarkup());
                    cells.Add(string.IsNullOrEmpty(review.ResponseText) ? string.Empty : "yes");

                    table.AddRow([.. cells]);
                }

                return table;
            }

            private static string FormatRating(double? rating)
            {
                if (!rating.HasValue)
                {
                    return string.Empty;
                }

                var stars = (int)Math.Round(rating.Value, MidpointRounding.AwayFromZero);
                stars = Math.Clamp(stars, 0, 5);

                return $"{new string('*', stars)}{new string('-', 5 - stars)} ({rating.Value.ToString("0.#", CultureInfo.InvariantCulture)})";
            }

            private static string Truncate(string value)
            {
                // Reviews are free-form and can contain newlines, which would break the row layout.
                value = value.ReplaceLineEndings(" ");

                return value.Length <= MaxTextLengthInTable
                    ? value
                    : string.Concat(value.AsSpan(0, MaxTextLengthInTable), "...");
            }
        }
    }
}
