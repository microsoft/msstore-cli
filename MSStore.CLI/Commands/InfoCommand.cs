// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class InfoCommand : Command
    {
        public InfoCommand()
            : base("info", "Print existing configuration.")
        {
        }

        public new class Handler : ICommandHandler
        {
            private readonly IConfigurationManager<Configurations> _configurationManager;
            private readonly TelemetryClient _telemetryClient;
            private readonly ILogger _logger;

            public Handler(IConfigurationManager<Configurations> configurationManager, TelemetryClient telemetryClient, ILogger<Handler> logger)
            {
                _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                var config = await _configurationManager.LoadAsync(ct: ct);

                var table = new Table();
                table.Title = new TableTitle($"[b]Current Config[/]");

                table.AddColumns("Config", "Value");

                table.AddRow($"[bold u]Seller Id[/]", $"[bold u]{config.SellerId}[/]");
                table.AddRow($"[bold u]Tenant Id[/]", $"[bold u]{config.TenantId}[/]");
                table.AddRow($"[bold u]Client Id[/]", $"[bold u]{config.ClientId}[/]");

                bool verbose = context.ParseResult.IsVerbose();

                if (verbose && !string.IsNullOrEmpty(config.StoreApiServiceUrl))
                {
                    table.AddRow($"[bold u]Store API Service Url[/]", $"[bold u]{config.StoreApiServiceUrl}[/]");
                }

                if (verbose && !string.IsNullOrEmpty(config.StoreApiScope))
                {
                    table.AddRow($"[bold u]Store API Scope[/]", $"[bold u]{config.StoreApiScope}[/]");
                }

                if (verbose && !string.IsNullOrEmpty(config.DevCenterServiceUrl))
                {
                    table.AddRow($"[bold u]Dev Center Service Url[/]", $"[bold u]{config.DevCenterServiceUrl}[/]");
                }

                if (verbose && !string.IsNullOrEmpty(config.DevCenterScope))
                {
                    table.AddRow($"[bold u]Dev Center Scope[/]", $"[bold u]{config.DevCenterScope}[/]");
                }

                if (verbose)
                {
                    _logger.LogInformation("Settings File Path: {@SettingsFilePath}", _configurationManager.ConfigPath);
                }

                AnsiConsole.Write(table);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
            }
        }
    }
}
