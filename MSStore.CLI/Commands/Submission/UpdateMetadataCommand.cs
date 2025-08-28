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
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class UpdateMetadataCommand : Command
    {
        private static readonly Argument<string> MetadataArgument;

        static UpdateMetadataCommand()
        {
            MetadataArgument = new Argument<string>("metadata")
            {
                Description = "The updated JSON metadata representation."
            };
        }

        public UpdateMetadataCommand()
            : base("updateMetadata", "Updates the existing draft submission metadata with the provided JSON.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(MetadataArgument);
            Options.Add(SubmissionCommand.SkipInitialPolling);
        }

        public class Handler(ILogger<UpdateMetadataCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var metadata = parseResult.GetRequiredValue(MetadataArgument);
                var skipInitialPolling = parseResult.GetRequiredValue(SubmissionCommand.SkipInitialPolling);

                object? updateSubmissionData = null;

                if (ProductTypeHelper.Solve(productId) == ProductType.Packaged)
                {
                    updateSubmissionData = await UpdateCommand.Handler.PackagedUpdateCommandAsync(_ansiConsole, _storeAPIFactory, metadata, productId, _logger, ct);
                }
                else
                {
                    var submissionMetadata = JsonSerializer.Deserialize(metadata, SourceGenerationContext.GetCustom().UpdateMetadataRequest);

                    if (submissionMetadata == null)
                    {
                        throw new MSStoreException("Invalid metadata provided.");
                    }

                    updateSubmissionData = await _ansiConsole.Status().StartAsync("Updating submission metadata", async ctx =>
                    {
                        try
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            return await storeAPI.UpdateSubmissionMetadataAsync(productId, submissionMetadata, skipInitialPolling, ct);
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while updating submission metadata.");
                            ctx.ErrorStatus(_ansiConsole, err);
                            return null;
                        }
                    });
                }

                if (updateSubmissionData == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(updateSubmissionData, updateSubmissionData.GetType(), SourceGenerationContext.GetCustom(true)));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
            }
        }
    }
}
