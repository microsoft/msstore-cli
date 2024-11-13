// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
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
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(GetCommand.FlightIdArgument);
        }

        public new class Handler(ILogger<DeleteCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient) : ICommandHandler
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
                    await AnsiConsole.Status().StartAsync("Deleting Flight", async ctx =>
                    {
                        try
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            DevCenterError? devCenterError = await storePackagedAPI.DeleteFlightAsync(ProductId, FlightId, ct);

                            ctx.SuccessStatus("[bold green]Deleted Flight[/]");

                            return 0;
                        }
                        catch (MSStoreHttpException err)
                        {
                            _logger.LogError(err, "Could not delete the flight.");
                            ctx.ErrorStatus("Could not delete the flight.");

                            return -1;
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while deleting flight.");
                            ctx.ErrorStatus("Error while deleting flight. Please try again.");
                            return -1;
                        }
                    }), ct);
            }
        }
    }
}
