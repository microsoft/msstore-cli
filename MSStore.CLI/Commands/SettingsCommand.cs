// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Commands.Settings;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using MSStore.CLI.Services.Telemetry;

namespace MSStore.CLI.Commands
{
    internal class SettingsCommand : Command
    {
        private static readonly Option<bool?> EnableTelemetryOption;

        static SettingsCommand()
        {
            EnableTelemetryOption = new Option<bool?>("--enableTelemetry", "-t")
            {
                Description = "Enable (empty/true) or Disable (false) telemetry."
            };
        }

        public SettingsCommand(SetPublisherDisplayNameCommand setPublisherDisplayNameCommand)
            : base("settings", "Change settings of the Microsoft Store Developer CLI.")
        {
            Options.Add(EnableTelemetryOption);

            Subcommands.Add(setPublisherDisplayNameCommand);
        }

        public class Handler(TelemetryClient telemetryClient, IConfigurationManager<TelemetryConfigurations> telemetryConfigurationManager, IConfigurationManager<Configurations> configurationManager, ILogger<SettingsCommand.Handler> logger) : AsynchronousCommandLineAction
        {
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            private readonly IConfigurationManager<TelemetryConfigurations> _telemetryConfigurationManager = telemetryConfigurationManager ?? throw new ArgumentNullException(nameof(telemetryConfigurationManager));
            private readonly IConfigurationManager<Configurations> _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var enableTelemetry = parseResult.GetValue(EnableTelemetryOption);

                try
                {
                    var telemetryConfigurations = await _telemetryConfigurationManager.LoadAsync(true, ct);

                    if (!enableTelemetry.HasValue)
                    {
                        new HelpAction().Invoke(parseResult);

                        _logger.LogInformation("TelemetryEnabled = {TelemetryEnabled}", telemetryConfigurations.TelemetryEnabled);

                        var config = await _configurationManager.LoadAsync(ct: ct);
                        _logger.LogInformation("PublisherDisplayName = {PublisherDisplayName}", config.PublisherDisplayName);
                    }
                    else
                    {
                        telemetryConfigurations.TelemetryEnabled = enableTelemetry.Value;
                        _logger.LogInformation("TelemetryEnabled set to '{TelemetryEnabled}'", telemetryConfigurations.TelemetryEnabled);
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
