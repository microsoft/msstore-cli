// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;

namespace MSStore.CLI.Commands.Settings
{
    // To be removed when PartnerCenterManager.Enabled == true
    internal class SetPublisherDisplayNameCommand : Command
    {
        private static readonly Argument<string> PublisherDisplayNameArgument;

        static SetPublisherDisplayNameCommand()
        {
            PublisherDisplayNameArgument = new Argument<string>("publisherDisplayName")
            {
                Description = "The Publisher Display Name property that will be set globally."
            };
        }

        public SetPublisherDisplayNameCommand()
            : base("setpdn", "Set the Publisher Display Name property that is used by the init command.")
        {
            Arguments.Add(PublisherDisplayNameArgument);
        }

        public class Handler(
            ILogger<SetPublisherDisplayNameCommand.Handler> logger,
            IConfigurationManager<Configurations> configurationManager,
            TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IConfigurationManager<Configurations> _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var publisherDisplayName = parseResult.GetRequiredValue(PublisherDisplayNameArgument);

                try
                {
                    var config = await _configurationManager.LoadAsync(ct: ct);
                    config.PublisherDisplayName = publisherDisplayName;
                    await _configurationManager.SaveAsync(config, ct);

                    _logger.LogInformation("PublisherDisplayName set to '{PublisherDisplayName}'", publisherDisplayName);

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