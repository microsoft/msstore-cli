// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;

namespace MSStore.CLI.Commands
{
    internal class ReconfigureCommand : Command
    {
        public ReconfigureCommand()
            : base("reconfigure", "Re-configure the Microsoft Store Developer CLI.")
        {
            var tenantId = new Option<Guid>(
                aliases: new string[] { "--tenantId", "-t" },
                description: "Specify the tenant Id that should be used.");

            var sellerId = new Option<string>(
                aliases: new string[] { "--sellerId", "-s" },
                description: "Specify the seller Id that should be used.");

            var clientId = new Option<Guid>(
                aliases: new string[] { "--clientId", "-c" },
                description: "Specify the client Id that should be used.");

            var clientSecret = new Option<string>(
                aliases: new string[] { "--clientSecret", "-cs" },
                description: "Specify the client Secret that should be used.");

            var reset = new Option<bool>(
                aliases: new string[] { "--reset" },
                description: "Only reset the credentials, without starting over.");

            AddOption(tenantId);
            AddOption(sellerId);
            AddOption(clientId);
            AddOption(clientSecret);
            AddOption(reset);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ICLIConfigurator _cliConfigurator;
            private readonly TelemetryClient _telemetryClient;

            public Guid? TenantId { get; set; }
            public string? SellerId { get; set; }
            public Guid? ClientId { get; set; }
            public string? ClientSecret { get; set; }
            public bool? Reset { get; set; }

            public Handler(ICLIConfigurator cliConfigurator, TelemetryClient telemetryClient)
            {
                _cliConfigurator = cliConfigurator;
                _telemetryClient = telemetryClient;
            }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                bool askConfirmation = TenantId == null ||
                                       SellerId == null ||
                                       ClientId == null ||
                                       ClientSecret == null;

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    (Reset == true
                        ? await _cliConfigurator.ResetAsync(ct: ct)
                        : await _cliConfigurator.ConfigureAsync(
                            askConfirmation,
                            tenantId: TenantId,
                            sellerId: SellerId,
                            clientId: ClientId,
                            clientSecret: ClientSecret,
                            ct: ct)) ? 0 : -1,
                    new Dictionary<string, string>
                    {
                        { "reset", (Reset == true).ToString() },
                        { "withTenant", (TenantId != null).ToString() },
                        { "withSellerId", (SellerId != null).ToString() },
                        { "withClientId", (ClientId != null).ToString() },
                        { "withClientSecret", (ClientSecret != null).ToString() }
                    },
                    ct);
            }
        }
    }
}
