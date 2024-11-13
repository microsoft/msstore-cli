// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
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
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(Flights.GetCommand.FlightIdArgument);
        }

        public new class Handler(ILogger<StatusCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient) : ICommandHandler
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

                var devCenterFlightSubmission = await AnsiConsole.Status().StartAsync<DevCenterFlightSubmission?>("Retrieving flight submission status", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flight = await storePackagedAPI.GetFlightAsync(ProductId, FlightId, ct);

                        if (flight?.FlightId == null)
                        {
                            ctx.ErrorStatus($"Could not find application flight with ID '{ProductId}'/'{FlightId}'");
                            return null;
                        }

                        return await storePackagedAPI.GetAnyFlightSubmissionAsync(ProductId, flight, ctx, _logger, ct);
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving submission status.");
                        AnsiConsole.WriteLine("Error!");
                        return null;
                    }
                });

                if (devCenterFlightSubmission?.Id == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                if (devCenterFlightSubmission.Status != null)
                {
                    AnsiConsole.MarkupLine($"Submission Status = [green]{devCenterFlightSubmission.Status}[/]");
                }

                devCenterFlightSubmission.StatusDetails?.PrintAllTables(ProductId, devCenterFlightSubmission.Id, _logger);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, 0, ct);
            }
        }
    }
}
