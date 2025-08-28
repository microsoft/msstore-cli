// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights.Submission
{
    internal class StatusCommand : Command
    {
        public StatusCommand()
            : base("status", "Retrieves the current status of the store flight submission.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(Flights.GetCommand.FlightIdArgument);
        }

        public class Handler(ILogger<StatusCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var flightId = parseResult.GetRequiredValue(Flights.GetCommand.FlightIdArgument);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var devCenterFlightSubmission = await _ansiConsole.Status().StartAsync<DevCenterFlightSubmission?>("Retrieving flight submission status", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flight = await storePackagedAPI.GetFlightAsync(productId, flightId, ct);

                        if (flight?.FlightId == null)
                        {
                            ctx.ErrorStatus(_ansiConsole, $"Could not find application flight with ID '{productId}'/'{flightId}'");
                            return null;
                        }

                        return await storePackagedAPI.GetAnyFlightSubmissionAsync(_ansiConsole, productId, flight, ctx, _logger, ct);
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving submission status.");
                        _ansiConsole.WriteLine("Error!");
                        return null;
                    }
                });

                if (devCenterFlightSubmission?.Id == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                if (devCenterFlightSubmission.Status != null)
                {
                    ansiConsole.MarkupLine($"Submission Status = [green]{devCenterFlightSubmission.Status}[/]");
                }

                devCenterFlightSubmission.StatusDetails?.PrintAllTables(_ansiConsole, productId, devCenterFlightSubmission.Id, _logger);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
            }
        }
    }
}
