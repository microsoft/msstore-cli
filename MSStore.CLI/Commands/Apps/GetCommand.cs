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
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Apps
{
    internal class GetCommand : Command
    {
        public GetCommand()
            : base("get", "Retrieves the Application details.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
        }

        public class Handler(ILogger<GetCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                string productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                object? application = null;
                var success = await _ansiConsole.Status().StartAsync("Retrieving Application", async ctx =>
                {
                    try
                    {
                        if (ProductTypeHelper.Solve(productId) == ProductType.Packaged)
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            application = await storePackagedAPI.GetApplicationAsync(productId, ct);
                        }
                    }
                    catch (MSStoreHttpException err)
                    {
                        _logger.LogError(err, "Error while retrieving Application.");

                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not find the Application. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while retrieving Application.");
                        }

                        return false;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving Application.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return false;
                    }

                    return true;
                });

                if (!success)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                }

                if (application is DevCenterApplication app)
                {
                    if (app?.Id == null)
                    {
                        _ansiConsole.MarkupLine($"Could not find application with ID '{productId}'");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                    }
                    else
                    {
                        AnsiConsole.WriteLine(JsonSerializer.Serialize(app, app.GetType(), SourceGenerationContext.GetCustom(true)));
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                    }
                }


                return await _telemetryClient.TrackCommandEventAsync<Handler>(-2, ct);
            }
        }
    }
}
