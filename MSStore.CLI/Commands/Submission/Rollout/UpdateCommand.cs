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

namespace MSStore.CLI.Commands.Submission.Rollout
{
    internal class UpdateCommand : Command
    {
        private static readonly Argument<float> PercentageArgument;

        static UpdateCommand()
        {
            PercentageArgument = new Argument<float>("percentage")
            {
                Description = "The percentage of users that will receive the submission rollout."
            };
        }

        public UpdateCommand()
            : base("update", "Update the rollout percentage of a submission.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Options.Add(GetCommand.SubmissionIdOption);
            Arguments.Add(PercentageArgument);
        }

        public class Handler(ILogger<UpdateCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var submissionId = parseResult.GetValue(GetCommand.SubmissionIdOption);
                var percentage = parseResult.GetRequiredValue(PercentageArgument);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                if (percentage < 0 || percentage > 100)
                {
                    _ansiConsole.WriteLine("The percentage must be between 0 and 100.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var submissionRollout = await _ansiConsole.Status().StartAsync("Updating Submission Rollout", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        if (submissionId == null)
                        {
                            var application = await storePackagedAPI.GetApplicationAsync(productId, ct);

                            if (application?.Id == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find application with ID '{productId}'");
                                return null;
                            }

                            submissionId = application.GetAnySubmissionId();

                            if (submissionId == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, "Could not find the submission. Please check the ProductId.");
                                return null;
                            }
                        }

                        return await storePackagedAPI.UpdatePackageRolloutPercentageAsync(productId, submissionId, null, percentage, ct);
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not find the submission rollout. Please check the ProductId.");
                            _logger.LogError(err, "Could not find the submission rollout. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while retrieving submission rollout.");
                            _logger.LogError(err, "Error while retrieving submission rollout for Application.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving submission rollout.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (submissionRollout == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(submissionRollout, SourceGenerationContext.GetCustom(true).PackageRollout));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
            }
        }
    }
}
