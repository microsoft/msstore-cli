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
            AddArgument(SubmissionCommand.ProductIdArgument);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;

            public string ProductId { get; set; } = null!;

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

                object? application = null;
                var success = await AnsiConsole.Status().StartAsync("Retrieving Application", async ctx =>
                {
                    try
                    {
                        if (ProductTypeHelper.Solve(ProductId) == ProductType.Packaged)
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            application = await storePackagedAPI.GetApplicationAsync(ProductId, ct);
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            AnsiConsole.MarkupLine("[yellow b]Not supported yet![/]");
                        }
                    }
                    catch (MSStoreHttpException err)
                    {
                        _logger.LogError(err, "Error while retrieving Application.");

                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus("Could not find the Application. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus("Error while retrieving Application.");
                        }

                        return false;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving Application.");
                        ctx.ErrorStatus(err);
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
                        AnsiConsole.MarkupLine($"Could not find application with ID '{ProductId}'");
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
