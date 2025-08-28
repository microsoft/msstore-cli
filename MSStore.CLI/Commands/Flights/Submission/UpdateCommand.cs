// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Models;
using MSStore.API.Packaged;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights.Submission
{
    internal class UpdateCommand : Command
    {
        private static readonly Argument<string> ProductArgument;

        static UpdateCommand()
        {
            ProductArgument = new Argument<string>("product")
            {
                Description = "The updated JSON product representation."
            };
        }

        public UpdateCommand()
            : base("update", "Updates the existing flight draft with the provided JSON.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(Flights.GetCommand.FlightIdArgument);
            Arguments.Add(ProductArgument);
        }

        public class Handler(ILogger<UpdateCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var flightId = parseResult.GetRequiredValue(Flights.GetCommand.FlightIdArgument);
                var product = parseResult.GetRequiredValue(ProductArgument);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var updateFlightSubmission = JsonSerializer.Deserialize(product, SourceGenerationContext.GetCustom().DevCenterFlightSubmissionUpdate);

                if (updateFlightSubmission == null)
                {
                    throw new MSStoreException("Invalid product provided.");
                }

                IStorePackagedAPI storePackagedAPI = null!;

                var flight = await _ansiConsole.Status().StartAsync("Retrieving application flight", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flight = await storePackagedAPI.GetFlightAsync(productId, flightId, ct);

                        if (flight?.FlightId == null)
                        {
                            throw new MSStoreException($"Could not find application flight with ID '{productId}'/'{flightId}'");
                        }

                        return flight;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while updating submission product.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (storePackagedAPI == null || flight == null || flight?.FlightId == null)
                {
                    return 1;
                }

                string? submissionId = flight.PendingFlightSubmission?.Id;

                if (submissionId == null)
                {
                    _ansiConsole.MarkupLine("Could not find an existing flight submission. [b green]Creating new flight submission[/].");

                    var flightSubmission = await storePackagedAPI.CreateNewFlightSubmissionAsync(_ansiConsole, productId, flightId, _logger, ct);
                    submissionId = flightSubmission?.Id;

                    if (submissionId == null)
                    {
                        throw new MSStoreException("Could not create new flight submission.");
                    }
                }

                var updatedFlightSubmission = await _ansiConsole.Status().StartAsync("Updating flight submission product", async ctx =>
                {
                    try
                    {
                        return await storePackagedAPI.UpdateFlightSubmissionAsync(productId, flightId, submissionId, updateFlightSubmission, ct);
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while updating flight submission product.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (updatedFlightSubmission == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(updatedFlightSubmission, SourceGenerationContext.GetCustom(true).DevCenterFlightSubmission));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
            }
        }
    }
}
