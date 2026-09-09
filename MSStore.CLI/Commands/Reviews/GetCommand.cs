// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Models;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using MSStore.CLI.Services.Translation;
using Spectre.Console;

namespace MSStore.CLI.Commands.Reviews
{
    internal class GetCommand : Command
    {
        public GetCommand()
            : base("get", "Retrieves the details of a single review.")
        {
            Arguments.Add(ReviewsCommand.ProductIdArgument);
            Arguments.Add(ReviewsCommand.ReviewIdArgument);
            Options.Add(ReviewsCommand.StartDateOption);
            Options.Add(ReviewsCommand.EndDateOption);
            Options.Add(ReviewsCommand.TranslateOption);
        }

        public class Handler(
            ILogger<GetCommand.Handler> logger,
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
                string reviewId = parseResult.GetRequiredValue(ReviewsCommand.ReviewIdArgument);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var translateLanguage = parseResult.GetTranslateLanguage();

                AppReview? review = null;

                // A failed call and a review that genuinely is not in the result set both
                // leave 'review' null, so the outcome is tracked separately to avoid telling
                // the user the review does not exist when the call never succeeded.
                var success = await _ansiConsole.Status().StartAsync("Retrieving Review", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var response = await storePackagedAPI.GetAppReviewsAsync(
                            productId,
                            parseResult.GetValue(ReviewsCommand.StartDateOption),
                            parseResult.GetValue(ReviewsCommand.EndDateOption),
                            filter: $"id eq '{reviewId.Replace("'", "''", StringComparison.Ordinal)}'",
                            ct: ct);

                        review = response.Value?.Find(r => string.Equals(r.Id, reviewId, StringComparison.OrdinalIgnoreCase));

                        if (review != null && translateLanguage != null)
                        {
                            ctx.Status("Translating Review");
                            await ReviewTranslator.TranslateAsync(_translationService, [review], translateLanguage, ct);
                        }

                        ctx.SuccessStatus(_ansiConsole, "[bold green]Retrieved Review[/]");

                        return true;
                    }
                    catch (TranslationException err)
                    {
                        _logger.LogError(err, "Error while translating Review.");
                        ctx.ErrorStatus(_ansiConsole, err.Message);
                        return false;
                    }
                    catch (MSStoreHttpException err)
                    {
                        _logger.LogError(err, "Error while retrieving Review.");

                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not find the Application. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while retrieving Review.");
                        }

                        return false;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving Review.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return false;
                    }
                });

                if (!success)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                if (review == null)
                {
                    _ansiConsole.MarkupLine(parseResult.NarrowedReviewsByDate()
                        ? $"Could not find review with ID '{reviewId.EscapeMarkup()}' within the requested date range. Try widening it with [bold]--startDate[/] and [bold]--endDate[/]."
                        : $"Could not find review with ID '{reviewId.EscapeMarkup()}'.");

                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                StandardOutput.WriteLine(JsonSerializer.Serialize(review, SourceGenerationContext.GetCustom(true).AppReview));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
            }
        }
    }
}
