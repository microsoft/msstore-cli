// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
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
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(Flights.GetCommand.FlightIdArgument);
            AddOption(Commands.Submission.DeleteCommand.NoConfirmOption);
        }

        public new class Handler(ILogger<DeleteCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IConsoleReader consoleReader, IBrowserLauncher browserLauncher, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : ICommandHandler
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public string ProductId { get; set; } = null!;
            public string FlightId { get; set; } = null!;
            public bool? NoConfirm { get; set; }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                if (ProductTypeHelper.Solve(ProductId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                IStorePackagedAPI storePackagedAPI = null!;

                var flightSubmissionId = await _ansiConsole.Status().StartAsync("Retrieving Flight Submission", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flight = await storePackagedAPI.GetFlightAsync(ProductId, FlightId, ct);

                        if (flight?.FlightId == null)
                        {
                            ctx.ErrorStatus(_ansiConsole, $"Could not find application flight with ID '{ProductId}'/'{FlightId}'");
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
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                _ansiConsole.WriteLine($"Found Flight Submission with Id '{flightSubmissionId}'");
                if (NoConfirm == false && !await _consoleReader.YesNoConfirmationAsync("Do you want to delete the pending flight submission?", ct))
                {
                    return -2;
                }

                var success = await storePackagedAPI.DeleteSubmissionAsync(_ansiConsole, ProductId, FlightId, flightSubmissionId, _browserLauncher, _logger, ct);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, success ? 0 : -1, ct);
            }
        }
    }
}
