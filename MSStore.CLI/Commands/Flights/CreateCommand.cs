// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
        private static readonly Argument<string> FriendlyNameArgument;
        private static readonly Option<IEnumerable<string>> GroupIdsOption;
        private static readonly Option<string> RankHigherThanOption;

        static CreateCommand()
        {
            FriendlyNameArgument = new Argument<string>("friendlyName")
            {
                Description = "The friendly name of the flight."
            };

            GroupIdsOption = new Option<IEnumerable<string>>("--group-ids", "-g")
            {
                DefaultValueFactory = _ => Array.Empty<string>(),
                Description = "The group IDs to associate with the flight.",
                AllowMultipleArgumentsPerToken = true
            };
            GroupIdsOption.Validators.Add((result) =>
            {
                var groupIds = result.Tokens.Select(t => t.Value).ToList();
                if (groupIds.Count == 0)
                {
                    result.AddError("At least one group ID must be provided.");
                }
            });

            RankHigherThanOption = new Option<string>("--rank-higher-than", "-r")
            {
                Description = "The flight ID to rank higher than."
            };
        }

        public CreateCommand()
            : base("create", "Creates a flight for the specified Application and flight.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(FriendlyNameArgument);
            Options.Add(GroupIdsOption);
            Options.Add(RankHigherThanOption);
        }

        public class Handler(ILogger<CreateCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var friendlyName = parseResult.GetRequiredValue(FriendlyNameArgument);
                var groupIds = parseResult.GetRequiredValue(GroupIdsOption);
                var rankHigherThan = parseResult.GetValue(RankHigherThanOption);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                var flight = await _ansiConsole.Status().StartAsync("Creating Flight", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var flight = await storePackagedAPI.CreateFlightAsync(productId, friendlyName, [.. groupIds], rankHigherThan, ct);

                        ctx.SuccessStatus(_ansiConsole, "[bold green]Created Flight[/]");

                        return flight;
                    }
                    catch (MSStoreHttpException err)
                    {
                        _logger.LogError(err, "Could not create the flight.");
                        ctx.ErrorStatus(_ansiConsole, "Could not create the flight.");

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while creating flight.");
                        ctx.ErrorStatus(_ansiConsole, "Error while creating flight. Please try again.");
                        return null;
                    }
                });

                if (flight != null)
                {
                    AnsiConsole.WriteLine(JsonSerializer.Serialize(flight, SourceGenerationContext.GetCustom(true).DevCenterFlight));
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
            }
        }
    }
}
