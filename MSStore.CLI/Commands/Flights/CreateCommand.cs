// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Flights
{
    internal class CreateCommand : Command
    {
        public CreateCommand()
            : base("create", "Creates a flight for the specified Application and flight.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);

            AddArgument(new Argument<string>("friendlyName", "The friendly name of the flight."));
            var groupIdsOption = new Option<IEnumerable<string>>(
                aliases: new string[]
                {
                    "--group-ids",
                    "-g"
                },
                getDefaultValue: Array.Empty<string>,
                description: "The group IDs to associate with the flight.")
            {
                AllowMultipleArgumentsPerToken = true
            };
            groupIdsOption.AddValidator((result) =>
            {
                var groupIds = result.Tokens.Select(t => t.Value).ToList();
                if (groupIds.Count == 0)
                {
                    result.ErrorMessage = "At least one group ID must be provided.";
                }
            });

            AddOption(groupIdsOption);

            AddOption(new Option<string>(new[] { "--rank-higher-than", "-r" }, "The flight ID to rank higher than."));
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;

            public string ProductId { get; set; } = null!;
            public string FriendlyName { get; set; } = null!;
            public IEnumerable<string> GroupIds { get; set; } = null!;
            public string? RankHigherThan { get; set; }

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
                    await AnsiConsole.Status().StartAsync("Creating Flight", async ctx =>
                    {
                        try
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var flight = await storePackagedAPI.CreateFlightAsync(ProductId, FriendlyName, GroupIds.ToList(), RankHigherThan, ct);

                            ctx.SuccessStatus("[bold green]Created Flight[/]");

                            AnsiConsole.WriteLine(JsonSerializer.Serialize(flight, SourceGenerationContext.GetCustom(true).DevCenterFlight));
                            return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                        }
                        catch (MSStoreHttpException err)
                        {
                            _logger.LogError(err, "Could not create the flight.");
                            ctx.ErrorStatus("Could not create the flight.");

                            return -1;
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while creating flight.");
                            ctx.ErrorStatus("Error while creating flight. Please try again.");
                            return -1;
                        }
                    }), ct);
            }
        }
    }
}
