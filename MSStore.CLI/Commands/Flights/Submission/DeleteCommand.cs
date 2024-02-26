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
            : base("delete", "Deletes the package flight submission.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(Flights.GetCommand.FlightIdArgument);
            AddArgument(GetCommand.SubmissionIdArgument);
            AddOption(Commands.Submission.DeleteCommand.NoConfirmOption);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly IConsoleReader _consoleReader;
            private readonly IBrowserLauncher _browserLauncher;
            private readonly TelemetryClient _telemetryClient;

            public string ProductId { get; set; } = null!;
            public string FlightId { get; set; } = null!;
            public string SubmissionId { get; set; } = null!;
            public bool? NoConfirm { get; set; }

            public Handler(ILogger<Handler> logger, IStoreAPIFactory storeAPIFactory, IConsoleReader consoleReader, IBrowserLauncher browserLauncher, TelemetryClient telemetryClient)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
                _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
                _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            }

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

                IStorePackagedAPI storePackagedAPI = null!;

                var flightSubmission = await AnsiConsole.Status().StartAsync("Retrieving Flight Submission", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        return await storePackagedAPI.GetFlightSubmissionAsync(ProductId, FlightId, SubmissionId, ct);
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus("Could not find the flight submission. Please check the ProductId/FlightId/SubmissionId.");
                            _logger.LogError(err, "Could not find the flight submission. Please check the ProductId/FlightId/SubmissionId.");
                        }
                        else
                        {
                            ctx.ErrorStatus("Error while retrieving flight submission.");
                            _logger.LogError(err, "Error while retrieving flight submission for Application.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving flight submission.");
                        ctx.ErrorStatus(err);
                        return null;
                    }
                });

                if (flightSubmission == null)
                {
                    AnsiConsole.WriteLine("Could not find flight submission.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                AnsiConsole.WriteLine($"Found Flight Submission with Id '{flightSubmission.Id}'");
                if (NoConfirm == false && !await _consoleReader.YesNoConfirmationAsync("Do you want to delete the pending flight submission?", ct))
                {
                    return -2;
                }

                var success = await storePackagedAPI.DeleteSubmissionAsync(ProductId, FlightId, SubmissionId, _browserLauncher, _logger, ct);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, success ? 0 : -1, ct);
            }
        }
    }
}
