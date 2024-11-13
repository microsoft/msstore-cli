// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
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
            FlightIdArgument = new Argument<string>("flightId", "The flight Id.");
        }

        public GetCommand()
            : base("get", "Retrieves a flight for the specified Application and flight.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(FlightIdArgument);
        }

        public new class Handler(ILogger<GetCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient) : ICommandHandler
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public string ProductId { get; set; } = null!;
            public string FlightId { get; set; } = null!;

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                if (ProductTypeHelper.Solve(ProductId) == ProductType.Unpackaged)
                {
                    AnsiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    await AnsiConsole.Status().StartAsync("Retrieving Flight", async ctx =>
                    {
                        try
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var flight = await storePackagedAPI.GetFlightAsync(ProductId, FlightId, ct);

                            ctx.SuccessStatus("[bold green]Retrieved Flight[/]");

                            AnsiConsole.WriteLine(JsonSerializer.Serialize(flight, SourceGenerationContext.GetCustom(true).DevCenterFlight));
                            return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while retrieving Flight.");
                            ctx.ErrorStatus(err);
                            return -1;
                        }
                    }), ct);
            }
        }
    }
}
