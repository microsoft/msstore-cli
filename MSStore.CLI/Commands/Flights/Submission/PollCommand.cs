// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
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
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(Flights.GetCommand.FlightIdArgument);
        }

        public class Handler(ILogger<PollCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient, IBrowserLauncher browserLauncher) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var flightId = parseResult.GetRequiredValue(Flights.GetCommand.FlightIdArgument);

                IStorePackagedAPI? storePackagedAPI = null;

                DevCenterFlight? flight = null;
                ApplicationSubmissionInfo? flightSubmission = null;

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                DevCenterSubmissionStatusResponse? lastSubmissionStatus = await _ansiConsole.Status().StartAsync("Polling flight submission status", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        flight = await storePackagedAPI.GetFlightAsync(productId, flightId, ct);

                        if (flight?.FlightId == null)
                        {
                            ctx.ErrorStatus(_ansiConsole, $"Could not find application flight with ID '{productId}'/'{flightId}'");
                            return null;
                        }

                        flightSubmission = flight.PendingFlightSubmission;

                        if (flightSubmission?.Id == null)
                        {
                            ctx.ErrorStatus(_ansiConsole, $"Could not find flight submission for application flight with ID '{productId}'/'{flightId}'");
                            return null;
                        }

                        var lastSubmissionStatus = await storePackagedAPI.PollSubmissionStatusAsync(ansiConsole, productId, flight.FlightId, flightSubmission.Id, false, _logger, ct: ct);

                        ctx.SuccessStatus(_ansiConsole);

                        return lastSubmissionStatus;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while polling submission status");
                        ctx.ErrorStatus(_ansiConsole, err);
                    }

                    return null;
                });

                if (lastSubmissionStatus != null && storePackagedAPI != null && flight?.FlightId != null && flightSubmission?.Id != null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(
                        productId,
                        await storePackagedAPI.HandleLastSubmissionStatusAsync(_ansiConsole, lastSubmissionStatus, productId, flight.FlightId, flightSubmission.Id, _browserLauncher, _logger, ct),
                        ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
            }
        }
    }
}
