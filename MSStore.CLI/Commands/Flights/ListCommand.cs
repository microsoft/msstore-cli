// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Threading;
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
            Arguments.Add(SubmissionCommand.ProductIdArgument);
        }

        public class Handler(ILogger<ListCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var flightsList = await _ansiConsole.Status().StartAsync("Retrieving Flights", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flightsList = await storePackagedAPI.GetFlightsAsync(productId, ct);

                        ctx.SuccessStatus(_ansiConsole, "[bold green]Retrieved Flights[/]");

                        return flightsList;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving Flights.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

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
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }
                else
                {
                    _ansiConsole.MarkupLine("The application has [bold][u]no[/] Flights[/].");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                }
            }
        }
    }
}
