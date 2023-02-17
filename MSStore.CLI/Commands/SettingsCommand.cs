// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Commands.Settings;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using MSStore.CLI.Services.Telemetry;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class SettingsCommand : Command
    {
        public SettingsCommand()
            : base("settings", "Change settings of the Microsoft Store Developer CLI.")
        {
            var enableTelemetry = new Option<bool>(
                "--enableTelemetry",
                "Enable (empty/true) or Disable (false) telemetry.");
            enableTelemetry.AddAlias("-t");
            AddOption(enableTelemetry);

            AddCommand(new SetPublisherDisplayNameCommand());

            this.SetHandler(() =>
            {
            });
        }

        public new class Handler : ICommandHandler
        {
            private readonly TelemetryClient _telemetryClient;
            private readonly IConfigurationManager<TelemetryConfigurations> _telemetryConfigurationManager;
            private readonly IConfigurationManager<Configurations> _configurationManager;
            private readonly ILogger _logger;

            public bool? EnableTelemetry { get; set; }

            public Handler(TelemetryClient telemetryClient, IConfigurationManager<TelemetryConfigurations> telemetryConfigurationManager, IConfigurationManager<Configurations> configurationManager, ILogger<Handler> logger)
            {
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
                _telemetryConfigurationManager = telemetryConfigurationManager ?? throw new ArgumentNullException(nameof(telemetryConfigurationManager));
                _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                try
                {
                    var telemetryConfigurations = await _telemetryConfigurationManager.LoadAsync(true, ct);

                    if (!EnableTelemetry.HasValue)
                    {
                        HelpBuilder helpBuilder = new(LocalizationResources.Instance, CommandExtensions.GetBufferWidth());
                        helpBuilder.Write(context.ParseResult.CommandResult.Command, Console.Out);

                        _logger.LogInformation("TelemetryEnabled = {TelemetryEnabled}", telemetryConfigurations.TelemetryEnabled);

                        var config = await _configurationManager.LoadAsync(ct: ct);
                        _logger.LogInformation("PublisherDisplayName = {PublisherDisplayName}", config.PublisherDisplayName);
                    }
                    else
                    {
                        telemetryConfigurations.TelemetryEnabled = EnableTelemetry.Value;
                        await _telemetryConfigurationManager.SaveAsync(telemetryConfigurations, ct);
                    }

                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }
                catch
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                }
            }
        }
    }
}
