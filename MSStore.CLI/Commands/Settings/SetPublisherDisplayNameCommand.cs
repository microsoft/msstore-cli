// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;

namespace MSStore.CLI.Commands.Settings
{
    // To be removed when PartnerCenterManager.Enabled == true
    internal class SetPublisherDisplayNameCommand : Command
    {
        public SetPublisherDisplayNameCommand()
            : base("setpdn", "Set the Publisher Display Name property that is used by the init command.")
        {
            var publisherDisplayName = new Argument<string>("publisherDisplayName", "The Publisher Display Name property that will be set globally.");
            AddArgument(publisherDisplayName);
        }

        public new class Handler : ICommandHandler
        {
            private readonly IConfigurationManager<Configurations> _configurationManager;
            private readonly TelemetryClient _telemetryClient;

            public string? PublisherDisplayName { get; set; }

            public Handler(
                IConfigurationManager<Configurations> configurationManager,
                TelemetryClient telemetryClient)
            {
                _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
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
                    var config = await _configurationManager.LoadAsync(ct: ct);
                    config.PublisherDisplayName = PublisherDisplayName;
                    await _configurationManager.SaveAsync(config, ct);

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