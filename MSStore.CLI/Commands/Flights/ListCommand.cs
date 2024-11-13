// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights
{
    internal class ListCommand : Command
    {
        public ListCommand()
            : base("list", "Retrieves all the Flights for the specified Application.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
        }

        public new class Handler(ILogger<ListCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient) : ICommandHandler
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public string ProductId { get; set; } = null!;

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
                    await AnsiConsole.Status().StartAsync("Retrieving Flights", async ctx =>
                    {
                        try
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var flightsList = await storePackagedAPI.GetFlightsAsync(ProductId, ct);

                            ctx.SuccessStatus("[bold green]Retrieved Flights[/]");

                            if (flightsList?.Count > 0)
                            {
                                var table = new Table();
                                table.AddColumns(string.Empty, "FlightId", "FriendlyName", "LastPublishedFlightSubmission.Id", "PendingFlightSubmission.Id", "GroupIds", "RankHigherThan");

                                int i = 1;
                                foreach (var f in flightsList)
                                {
                                    table.AddRow(
                                        i.ToString(CultureInfo.InvariantCulture),
                                        $"[bold u]{f.FlightId}[/]",
                                        $"[bold u]{f.FriendlyName}[/]",
                                        $"[bold u]{f.LastPublishedFlightSubmission?.Id}[/]",
                                        $"[bold u]{f.PendingFlightSubmission?.Id}[/]",
                                        $"[bold u]{string.Join(", ", f.GroupIds ?? [])}[/]",
                                        $"[bold u]{f.RankHigherThan}[/]");
                                    i++;
                                }

                                AnsiConsole.Write(table);
                            }
                            else
                            {
                                AnsiConsole.MarkupLine("The application has [bold][u]no[/] Flights[/].");
                            }

                            AnsiConsole.WriteLine();

                            return 0;
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while retrieving Flights.");
                            ctx.ErrorStatus(err);
                            return -1;
                        }
                    }), ct);
            }
        }
    }
}
