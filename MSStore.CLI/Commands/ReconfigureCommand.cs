// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class ReconfigureCommand : Command
    {
        private static readonly Option<Guid?> TenantIdOption;
        private static readonly Option<string> SellerIdOption;
        private static readonly Option<Guid?> ClientIdOption;
        private static readonly Option<string> ClientSecretOption;
        private static readonly Option<string> CertificateThumbprintOption;
        private static readonly Option<FileInfo?> CertificateFilePathOption;
        private static readonly Option<string> CertificatePasswordOption;
        private static readonly Option<bool> ResetOption;

        static ReconfigureCommand()
        {
            TenantIdOption = new Option<Guid?>("--tenantId", "-t")
            {
                Description = "Specify the tenant Id that should be used."
            };

            SellerIdOption = new Option<string>("--sellerId", "-s")
            {
                Description = "Specify the seller Id that should be used."
            };

            ClientIdOption = new Option<Guid?>("--clientId", "-c")
            {
                Description = "Specify the client Id that should be used."
            };

            ClientSecretOption = new Option<string>("--clientSecret", "-cs")
            {
                Description = "Specify the client Secret that should be used."
            };

            CertificateThumbprintOption = new Option<string>("--certificateThumbprint", "-ct")
            {
                Description = "Specify the certificate Thumbprint that should be used."
            };

            CertificateFilePathOption = new Option<FileInfo?>("--certificateFilePath", "-cfp")
            {
                Description = "Specify the certificate file path that should be used."
            };

            CertificatePasswordOption = new Option<string>("--certificatePassword", "-cp")
            {
                Description = "Specify the certificate password that should be used."
            };

            ResetOption = new Option<bool>("--reset")
            {
                Description = "Only reset the credentials, without starting over."
            };
        }

        public ReconfigureCommand()
            : base("reconfigure", "Re-configure the Microsoft Store Developer CLI.")
        {
            Options.Add(TenantIdOption);
            Options.Add(SellerIdOption);
            Options.Add(ClientIdOption);
            Options.Add(ClientSecretOption);
            Options.Add(CertificateThumbprintOption);
            Options.Add(CertificateFilePathOption);
            Options.Add(CertificatePasswordOption);
            Options.Add(ResetOption);
        }

        public class Handler(ICLIConfigurator cliConfigurator, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ICLIConfigurator _cliConfigurator = cliConfigurator;
            private readonly IAnsiConsole _ansiConsole = ansiConsole;
            private readonly TelemetryClient _telemetryClient = telemetryClient;

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var tenantId = parseResult.GetValue(TenantIdOption);
                var sellerId = parseResult.GetValue(SellerIdOption);
                var clientId = parseResult.GetValue(ClientIdOption);
                var clientSecret = parseResult.GetValue(ClientSecretOption);
                var certificateThumbprint = parseResult.GetValue(CertificateThumbprintOption);
                var certificateFilePath = parseResult.GetValue(CertificateFilePathOption);
                var certificatePassword = parseResult.GetValue(CertificatePasswordOption);
                var reset = parseResult.GetValue(ResetOption);

                bool askConfirmation = tenantId == null ||
                                       sellerId == null ||
                                       clientId == null ||
                                       (clientSecret == null &&
                                        certificateThumbprint == null &&
                                        certificateFilePath == null);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    (reset == true
                        ? await _cliConfigurator.ResetAsync(ct: ct)
                        : await _cliConfigurator.ConfigureAsync(
                            _ansiConsole,
                            askConfirmation,
                            tenantId: tenantId,
                            sellerId: sellerId,
                            clientId: clientId,
                            clientSecret: clientSecret,
                            certificateThumbprint: certificateThumbprint,
                            certificateFilePath: certificateFilePath?.FullName,
                            certificatePassword: certificatePassword,
                            ct: ct)) ? 0 : -1,
                    new Dictionary<string, string>
                    {
                        { "reset", (reset == true).ToString() },
                        { "withTenant", (tenantId != null).ToString() },
                        { "withSellerId", (sellerId != null).ToString() },
                        { "withClientId", (clientId != null).ToString() },
                        { "withClientSecret", (clientSecret != null).ToString() },
                        { "withCertificateThumbprint", (certificateThumbprint != null).ToString() },
                        { "withCertificateFilePath", (certificateFilePath != null).ToString() },
                        { "withCertificatePassword", (certificatePassword != null).ToString() }
                    },
                    ct);
            }
        }
    }
}
