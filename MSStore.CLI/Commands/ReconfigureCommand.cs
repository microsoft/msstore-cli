// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class ReconfigureCommand : Command
    {
        public ReconfigureCommand()
            : base("reconfigure", "Re-configure the Microsoft Store Developer CLI.")
        {
            var tenantId = new Option<Guid>(
                aliases: ["--tenantId", "-t"],
                description: "Specify the tenant Id that should be used.");

            var sellerId = new Option<string>(
                aliases: ["--sellerId", "-s"],
                description: "Specify the seller Id that should be used.");

            var clientId = new Option<Guid>(
                aliases: ["--clientId", "-c"],
                description: "Specify the client Id that should be used.");

            var clientSecret = new Option<string>(
                aliases: ["--clientSecret", "-cs"],
                description: "Specify the client Secret that should be used.");

            var certificateThumbprint = new Option<string>(
                aliases: ["--certificateThumbprint", "-ct"],
                description: "Specify the certificate Thumbprint that should be used.");

            var certificateFilePath = new Option<FileInfo?>(
                aliases: ["--certificateFilePath", "-cfp"],
                description: "Specify the certificate file path that should be used.");

            var certificatePassword = new Option<string>(
                aliases: ["--certificatePassword", "-cp"],
                description: "Specify the certificate password that should be used.");

            var reset = new Option<bool>(
                aliases: ["--reset"],
                description: "Only reset the credentials, without starting over.");

            AddOption(tenantId);
            AddOption(sellerId);
            AddOption(clientId);
            AddOption(clientSecret);
            AddOption(certificateThumbprint);
            AddOption(certificateFilePath);
            AddOption(certificatePassword);
            AddOption(reset);
        }

        public new class Handler(ICLIConfigurator cliConfigurator, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : ICommandHandler
        {
            private readonly ICLIConfigurator _cliConfigurator = cliConfigurator;
            private readonly IAnsiConsole _ansiConsole = ansiConsole;
            private readonly TelemetryClient _telemetryClient = telemetryClient;

            public Guid? TenantId { get; set; }
            public string? SellerId { get; set; }
            public Guid? ClientId { get; set; }
            public string? ClientSecret { get; set; }
            public string? CertificateThumbprint { get; set; }
            public FileInfo? CertificateFilePath { get; set; }
            public string? CertificatePassword { get; set; }
            public bool? Reset { get; set; }

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
                                       (ClientSecret == null &&
                                        CertificateThumbprint == null &&
                                        CertificateFilePath == null);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    (Reset == true
                        ? await _cliConfigurator.ResetAsync(ct: ct)
                        : await _cliConfigurator.ConfigureAsync(
                            _ansiConsole,
                            askConfirmation,
                            tenantId: TenantId,
                            sellerId: SellerId,
                            clientId: ClientId,
                            clientSecret: ClientSecret,
                            certificateThumbprint: CertificateThumbprint,
                            certificateFilePath: CertificateFilePath?.FullName,
                            certificatePassword: CertificatePassword,
                            ct: ct)) ? 0 : -1,
                    new Dictionary<string, string>
                    {
                        { "reset", (Reset == true).ToString() },
                        { "withTenant", (TenantId != null).ToString() },
                        { "withSellerId", (SellerId != null).ToString() },
                        { "withClientId", (ClientId != null).ToString() },
                        { "withClientSecret", (ClientSecret != null).ToString() },
                        { "withCertificateThumbprint", (CertificateThumbprint != null).ToString() },
                        { "withCertificateFilePath", (CertificateFilePath != null).ToString() },
                        { "withCertificatePassword", (CertificatePassword != null).ToString() }
                    },
                    ct);
            }
        }
    }
}
