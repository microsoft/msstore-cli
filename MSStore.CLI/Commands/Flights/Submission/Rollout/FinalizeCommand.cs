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
    internal class FinalizeCommand : Command
    {
        public FinalizeCommand()
            : base("finalize", "Finalizes the flight rollout of a submission.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(Flights.GetCommand.FlightIdArgument);
            AddOption(Commands.Submission.Rollout.GetCommand.SubmissionIdOption);
        }

        public new class Handler(ILogger<FinalizeCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : ICommandHandler
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public string ProductId { get; set; } = null!;
            public string FlightId { get; set; } = null!;
            public string? SubmissionId { get; set; }

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

                var flightSubmissionRollout = await _ansiConsole.Status().StartAsync("Finalizing Flight Submission Rollout", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        if (SubmissionId == null)
                        {
                            var flight = await storePackagedAPI.GetFlightAsync(ProductId, FlightId, ct);

                            if (flight?.FlightId == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find application flight with ID '{ProductId}'/'{FlightId}'");
                                return null;
                            }

                            SubmissionId = flight.GetAnyFlightSubmissionId();

                            if (SubmissionId == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, "Could not find the flight submission. Please check the ProductId/FlightId.");
                                return null;
                            }
                        }

                        return await storePackagedAPI.FinalizePackageRolloutAsync(ProductId, SubmissionId, FlightId, ct);
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not find the flight submission rollout. Please check the ProductId/FlightId.");
                            _logger.LogError(err, "Could not find the flight submission rollout. Please check the ProductId/FlightId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while finalizing flight submission rollout.");
                            _logger.LogError(err, "Error while finalizing flight submission rollout for Application.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while finalizing flight submission rollout.");
                        ctx.ErrorStatus(_ansiConsole, err);
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
