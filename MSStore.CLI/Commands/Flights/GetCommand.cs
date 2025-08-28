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
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights
{
    internal class GetCommand : Command
    {
        internal static readonly Argument<string> FlightIdArgument;

        static GetCommand()
        {
            FlightIdArgument = new Argument<string>("flightId")
            {
                Description = "The flight Id."
            };
        }

        public GetCommand()
            : base("get", "Retrieves a flight for the specified Application and flight.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(FlightIdArgument);
        }

        public class Handler(ILogger<GetCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var flightId = parseResult.GetRequiredValue(FlightIdArgument);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var flight = await _ansiConsole.Status().StartAsync("Retrieving Flight", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flight = await storePackagedAPI.GetFlightAsync(productId, flightId, ct);

                        ctx.SuccessStatus(_ansiConsole, "[bold green]Retrieved Flight[/]");

                        return flight;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving Flight.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (flight != null)
                {
                    AnsiConsole.WriteLine(JsonSerializer.Serialize(flight, SourceGenerationContext.GetCustom(true).DevCenterFlight));
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
            }
        }
    }
}
