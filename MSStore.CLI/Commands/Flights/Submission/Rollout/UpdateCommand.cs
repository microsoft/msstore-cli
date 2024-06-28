// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights.Submission.Rollout
{
    internal class UpdateCommand : Command
    {
        public UpdateCommand()
            : base("update", "Update the flight rollout percentage of a submission.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(Flights.GetCommand.FlightIdArgument);
            AddOption(Commands.Submission.Rollout.GetCommand.SubmissionIdOption);

            var percentage = new Argument<float>(
                "percentage",
                description: "The percentage of users that will receive the submission rollout.");
            AddArgument(percentage);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;

            public string ProductId { get; set; } = null!;
            public string FlightId { get; set; } = null!;
            public string? SubmissionId { get; set; }
            public float Percentage { get; set; }

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

                var flightSubmissionRollout = await AnsiConsole.Status().StartAsync("Updating Flight Submission Rollout", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        if (SubmissionId == null)
                        {
                            var flight = await storePackagedAPI.GetFlightAsync(ProductId, FlightId, ct);

                            if (flight?.FlightId == null)
                            {
                                ctx.ErrorStatus($"Could not find application flight with ID '{ProductId}'/'{FlightId}'");
                                return null;
                            }

                            SubmissionId = flight.GetAnyFlightSubmissionId();

                            if (SubmissionId == null)
                            {
                                ctx.ErrorStatus("Could not find the flight submission. Please check the ProductId/FlightId.");
                                return null;
                            }
                        }

                        return await storePackagedAPI.UpdatePackageRolloutPercentageAsync(ProductId, SubmissionId, FlightId, Percentage, ct);
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus("Could not find the flight submission rollout. Please check the ProductId/FlightId.");
                            _logger.LogError(err, "Could not find the flight submission rollout. Please check the ProductId/FlightId.");
                        }
                        else
                        {
                            ctx.ErrorStatus("Error while retrieving flight submission rollout.");
                            _logger.LogError(err, "Error while retrieving flight submission rollout for Application.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving flight submission rollout.");
                        ctx.ErrorStatus(err);
                        return null;
                    }
                });

                if (flightSubmissionRollout == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(flightSubmissionRollout, SourceGenerationContext.GetCustom(true).PackageRollout));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, 0, ct);
            }
        }
    }
}
