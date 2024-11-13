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

        public new class Handler(ILogger<GetCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient) : ICommandHandler
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public string ProductId { get; set; } = null!;

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
