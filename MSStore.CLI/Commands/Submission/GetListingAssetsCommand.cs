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
using MSStore.API.Models;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class GetListingAssetsCommand : Command
    {
        public GetListingAssetsCommand()
            : base("getListingAssets", "Retrieves the existing draft listing assets from the store submission.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Options.Add(SubmissionCommand.LanguageOption);
        }

        public class Handler(ILogger<GetListingAssetsCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var language = parseResult.GetRequiredValue(SubmissionCommand.LanguageOption);

                var ret = await _ansiConsole.Status().StartAsync<object?>("Retrieving listing assets", async ctx =>
                {
                    try
                    {
                        if (ProductTypeHelper.Solve(productId) == ProductType.Packaged)
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var application = await storePackagedAPI.GetApplicationAsync(productId, ct);

                            if (application?.Id == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find application with ID '{productId}'");
                                return -1;
                            }

                            var submission = await storePackagedAPI.GetAnySubmissionAsync(_ansiConsole, application, ctx, _logger, ct);

                            return submission;
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            var draft = await storeAPI.GetDraftListingAssetsAsync(productId, language, ct);

                            ctx.SuccessStatus(_ansiConsole);

                            return draft;
                        }
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving listing assets.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (ret is DevCenterSubmission submission)
                {
                    var listings = submission.Listings;

                    if (listings == null)
                    {
                        _ansiConsole.MarkupLine($":collision: [bold red]Submission has no listings.[/]");

                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                    }

                    foreach (var listing in listings)
                    {
                        _ansiConsole.WriteLine();
                        _ansiConsole.MarkupLine($"Listing [green b u]{listing.Key}[/]:");
                        var baseListing = listing.Value?.BaseListing;
                        if (baseListing != null)
                        {
                            AnsiConsole.WriteLine(JsonSerializer.Serialize(baseListing, baseListing.GetType(), SourceGenerationContext.GetCustom(true)));
                        }
                    }

                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }
                else if (ret is ListingAssetsResponse draft)
                {
                    AnsiConsole.WriteLine(JsonSerializer.Serialize(draft, draft.GetType(), SourceGenerationContext.GetCustom(true)));
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
            }
        }
    }
}
