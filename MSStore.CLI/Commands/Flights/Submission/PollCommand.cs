// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights.Submission
{
    internal class PollCommand : Command
    {
        public PollCommand()
            : base("poll", "Polls until the existing flight submission is PUBLISHED or FAILED.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(Flights.GetCommand.FlightIdArgument);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;
            private readonly IBrowserLauncher _browserLauncher;

            public string ProductId { get; set; } = null!;
            public string FlightId { get; set; } = null!;

            public Handler(ILogger<Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient, IBrowserLauncher browserLauncher)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
                _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                IStorePackagedAPI? storePackagedAPI = null;

                DevCenterFlight? flight = null;
                ApplicationSubmissionInfo? flightSubmission = null;

                if (ProductTypeHelper.Solve(ProductId) == ProductType.Unpackaged)
                {
                    AnsiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                DevCenterSubmissionStatusResponse? lastSubmissionStatus = await AnsiConsole.Status().StartAsync("Polling flight submission status", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        flight = await storePackagedAPI.GetFlightAsync(ProductId, FlightId, ct);

                        if (flight?.FlightId == null)
                        {
                            ctx.ErrorStatus($"Could not find application flight with ID '{ProductId}'/'{FlightId}'");
                            return null;
                        }

                        flightSubmission = flight.PendingFlightSubmission;

                        if (flightSubmission?.Id == null)
                        {
                            ctx.ErrorStatus($"Could not find flight submission for application flight with ID '{ProductId}'/'{FlightId}'");
                            return null;
                        }

                        var lastSubmissionStatus = await storePackagedAPI.PollSubmissionStatusAsync(ProductId, flight.FlightId, flightSubmission.Id, false, _logger, ct: ct);

                        ctx.SuccessStatus();

                        return lastSubmissionStatus;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while polling submission status");
                        ctx.ErrorStatus(err);
                    }

                    return null;
                });

                if (lastSubmissionStatus != null && storePackagedAPI != null && flight?.FlightId != null && flightSubmission?.Id != null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(
                        ProductId,
                        await storePackagedAPI.HandleLastSubmissionStatusAsync(lastSubmissionStatus, ProductId, flight.FlightId, flightSubmission.Id, _browserLauncher, _logger, ct),
                        ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
            }
        }
    }
}
