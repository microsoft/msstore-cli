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
    internal class HaltCommand : Command
    {
        public HaltCommand()
            : base("halt", "Halts the flight rollout of a submission.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(Flights.GetCommand.FlightIdArgument);
            Options.Add(Commands.Submission.Rollout.GetCommand.SubmissionIdOption);
        }

        public class Handler(ILogger<HaltCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
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

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var flightSubmissionRollout = await _ansiConsole.Status().StartAsync("Halting Flight Submission Rollout", async ctx =>
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

                        return await storePackagedAPI.HaltPackageRolloutAsync(productId, submissionId, flightId, ct);
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
                            ctx.ErrorStatus(_ansiConsole, "Error while halting flight submission rollout.");
                            _logger.LogError(err, "Error while halting flight submission rollout for Application.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while halting flight submission rollout.");
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
