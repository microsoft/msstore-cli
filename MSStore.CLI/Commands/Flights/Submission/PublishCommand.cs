// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights.Submission
{
    internal class PublishCommand : Command
    {
        public PublishCommand()
            : base("publish", "Starts the flight submission process for the existing Draft.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(Flights.GetCommand.FlightIdArgument);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;

            public string ProductId { get; set; } = null!;
            public string FlightId { get; set; } = null!;

            public Handler(ILogger<Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
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

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    ProductId,
                    await AnsiConsole.Status().StartAsync("Publishing flight submission", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flight = await storePackagedAPI.GetFlightAsync(ProductId, FlightId, ct);

                        if (flight?.FlightId == null)
                        {
                            ctx.ErrorStatus($"Could not find application flight with ID '{ProductId}'/'{FlightId}'");
                            return -1;
                        }

                        var flightSubmission = flight.PendingFlightSubmission;

                        if (flightSubmission?.Id == null)
                        {
                            ctx.ErrorStatus($"Could not find flight submission for application flight with ID '{ProductId}'/'{FlightId}'");
                            return -1;
                        }

                        var flightSubmissionCommit = await storePackagedAPI.CommitFlightSubmissionAsync(ProductId, FlightId, flightSubmission.Id, ct);

                        if (flightSubmissionCommit == null)
                        {
                            throw new MSStoreException("Flight Submission commit failed");
                        }

                        if (flightSubmissionCommit.Status != null)
                        {
                            ctx.SuccessStatus($"Flight Submission Commited with status [green u]{flightSubmissionCommit.Status}[/]");
                            return 0;
                        }

                        ctx.ErrorStatus($"Could not commit flight submission for application flight with ID '{ProductId}'/'{FlightId}'");
                        AnsiConsole.MarkupLine($"[red]{flightSubmissionCommit.ToErrorMessage()}[/]");

                        return -1;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while publishing flight submission");
                        ctx.ErrorStatus(err);
                        return -1;
                    }
                }), ct);
            }
        }
    }
}
