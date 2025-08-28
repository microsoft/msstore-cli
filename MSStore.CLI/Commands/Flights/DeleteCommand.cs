// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights
{
    internal class DeleteCommand : Command
    {
        public DeleteCommand()
            : base("delete", "Deletes a flight for the specified Application and flight.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(GetCommand.FlightIdArgument);
        }

        public class Handler(ILogger<DeleteCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var flightId = parseResult.GetRequiredValue(GetCommand.FlightIdArgument);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    await _ansiConsole.Status().StartAsync("Deleting Flight", async ctx =>
                    {
                        try
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            DevCenterError? devCenterError = await storePackagedAPI.DeleteFlightAsync(productId, flightId, ct);

                            ctx.SuccessStatus(_ansiConsole, "[bold green]Deleted Flight[/]");

                            return 0;
                        }
                        catch (MSStoreHttpException err)
                        {
                            _logger.LogError(err, "Could not delete the flight.");
                            ctx.ErrorStatus(_ansiConsole, "Could not delete the flight.");

                            return -1;
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while deleting flight.");
                            ctx.ErrorStatus(_ansiConsole, "Error while deleting flight. Please try again.");
                            return -1;
                        }
                    }), ct);
            }
        }
    }
}
