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
using MSStore.API.Packaged;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights.Submission
{
    internal class DeleteCommand : Command
    {
        public DeleteCommand()
            : base("delete", "Deletes the pending package flight submission from the store.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(Flights.GetCommand.FlightIdArgument);
            Options.Add(Commands.Submission.DeleteCommand.NoConfirmOption);
        }

        public class Handler(ILogger<DeleteCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IConsoleReader consoleReader, IBrowserLauncher browserLauncher, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var flightId = parseResult.GetRequiredValue(Flights.GetCommand.FlightIdArgument);
                var noConfirm = parseResult.GetValue(Commands.Submission.DeleteCommand.NoConfirmOption);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                IStorePackagedAPI storePackagedAPI = null!;

                var flightSubmissionId = await _ansiConsole.Status().StartAsync("Retrieving Flight Submission", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flight = await storePackagedAPI.GetFlightAsync(productId, flightId, ct);

                        if (flight?.FlightId == null)
                        {
                            ctx.ErrorStatus(_ansiConsole, $"Could not find application flight with ID '{productId}'/'{flightId}'");
                            return null;
                        }

                        if (flight.PendingFlightSubmission?.Id != null)
                        {
                            ctx.SuccessStatus(_ansiConsole, $"Found [green]Pending Flight Submission[/].");
                            return flight.PendingFlightSubmission.Id;
                        }

                        return null;
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not find the flight submission. Please check the ProductId/FlightId/SubmissionId.");
                            _logger.LogError(err, "Could not find the flight submission. Please check the ProductId/FlightId/SubmissionId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while retrieving flight submission.");
                            _logger.LogError(err, "Error while retrieving flight submission for Application.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving flight submission.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (flightSubmissionId == null)
                {
                    _ansiConsole.WriteLine("Could not find flight submission.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                _ansiConsole.WriteLine($"Found Flight Submission with Id '{flightSubmissionId}'");
                if (noConfirm == false && !await _consoleReader.YesNoConfirmationAsync("Do you want to delete the pending flight submission?", ct))
                {
                    return -2;
                }

                var success = await storePackagedAPI.DeleteSubmissionAsync(_ansiConsole, productId, flightId, flightSubmissionId, _browserLauncher, _logger, ct);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, success ? 0 : -1, ct);
            }
        }
    }
}
