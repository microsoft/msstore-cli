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

namespace MSStore.CLI.Commands.Flights.Submission.Rollout
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
            : base("update", "Update the flight rollout percentage of a submission.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(Flights.GetCommand.FlightIdArgument);
            Options.Add(Commands.Submission.Rollout.GetCommand.SubmissionIdOption);
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
                var flightId = parseResult.GetRequiredValue(Flights.GetCommand.FlightIdArgument);
                var submissionId = parseResult.GetValue(Commands.Submission.Rollout.GetCommand.SubmissionIdOption);
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

                var flightSubmissionRollout = await _ansiConsole.Status().StartAsync("Updating Flight Submission Rollout", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        if (submissionId == null)
                        {
                            var flight = await storePackagedAPI.GetFlightAsync(productId, flightId, ct);

                            if (flight?.FlightId == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find application flight with ID '{productId}'/'{flightId}'");
                                return null;
                            }

                            submissionId = flight.GetAnyFlightSubmissionId();

                            if (submissionId == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, "Could not find the flight submission. Please check the ProductId/FlightId.");
                                return null;
                            }
                        }

                        return await storePackagedAPI.UpdatePackageRolloutPercentageAsync(productId, submissionId, flightId, percentage, ct);
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not find the flight submission rollout. Please check the ProductId/FlightId.");
                            _logger.LogError(err, "Could not find the flight submission rollout. Please check the ProductId/FlightId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while retrieving flight submission rollout.");
                            _logger.LogError(err, "Error while retrieving flight submission rollout for Application.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving flight submission rollout.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (flightSubmissionRollout == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(flightSubmissionRollout, SourceGenerationContext.GetCustom(true).PackageRollout));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
            }
        }
    }
}
