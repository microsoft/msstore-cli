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
using MSStore.API.Packaged;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights.Submission
{
    internal class UpdateCommand : Command
    {
        public UpdateCommand()
            : base("update", "Updates the existing flight draft with the provided JSON.")
        {
            var product = new Argument<string>(
                "product",
                description: "The updated JSON product representation.");

            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(Flights.GetCommand.FlightIdArgument);
            AddArgument(product);
            AddOption(SubmissionCommand.SkipInitialPolling);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;

            public string Product { get; set; } = null!;
            public bool SkipInitialPolling { get; set; }
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

                var updateFlightSubmission = JsonSerializer.Deserialize(Product, SourceGenerationContext.GetCustom().DevCenterFlightSubmissionUpdate);

                if (updateFlightSubmission == null)
                {
                    throw new MSStoreException("Invalid product provided.");
                }

                IStorePackagedAPI storePackagedAPI = null!;

                var flight = await AnsiConsole.Status().StartAsync("Retrieving application flight", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flight = await storePackagedAPI.GetFlightAsync(ProductId, FlightId, ct);

                        if (flight?.FlightId == null)
                        {
                            throw new MSStoreException($"Could not find application flight with ID '{ProductId}'/'{FlightId}'");
                        }

                        return flight;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while updating submission product.");
                        ctx.ErrorStatus(err);
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
                    AnsiConsole.MarkupLine("Could not find an existing flight submission. [b green]Creating new flight submission[/].");

                    var flightSubmission = await storePackagedAPI.CreateNewFlightSubmissionAsync(ProductId, FlightId, _logger, ct);
                    submissionId = flightSubmission?.Id;

                    if (submissionId == null)
                    {
                        throw new MSStoreException("Could not create new flight submission.");
                    }
                }

                var updatedFlightSubmission = await AnsiConsole.Status().StartAsync("Updating flight submission product", async ctx =>
                {
                    try
                    {
                        return await storePackagedAPI.UpdateFlightSubmissionAsync(ProductId, FlightId, submissionId, updateFlightSubmission, ct);
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while updating flight submission product.");
                        ctx.ErrorStatus(err);
                        return null;
                    }
                });

                if (updatedFlightSubmission == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(updatedFlightSubmission, SourceGenerationContext.GetCustom(true).DevCenterFlightSubmission));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, 0, ct);
            }
        }
    }
}
