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
using MSStore.CLI.Commands;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using MSStore.CLI.Services.CredentialManager;
using Spectre.Console;

namespace MSStore.CLI
{
    internal class MicrosoftStoreCLI : RootCommand
    {
        internal static void WelcomeMessage()
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new FigletText("Microsoft Store Dev CLI")
                    .Color(Color.Blue));
            AnsiConsole.WriteLine();
        }

        internal Option<bool> VerboseOption { get; }

        public MicrosoftStoreCLI()
            : base(description: "CLI tool to automate Microsoft Store Developer tasks.")
        {
            VerboseOption = new Option<bool>(
                aliases: new string[] { "--verbose", "-v" },
                getDefaultValue: () => false,
                description: "Verbose output");
            AddGlobalOption(VerboseOption);

            AddCommand(new InfoCommand());
            AddCommand(new ReconfigureCommand());
            AddCommand(new SettingsCommand());
            AddCommand(new AppsCommand());
            AddCommand(new SubmissionCommand());
            AddCommand(new InitCommand());
            AddCommand(new PackageCommand());
            AddCommand(new PublishCommand());

            this.SetHandler(() =>
            {
            });
        }

        public new class Handler : ICommandHandler
        {
            private readonly IConfigurationManager<Configurations> _configurationManager;
            private readonly ICLIConfigurator _cliConfigurator;
            private readonly TelemetryClient _telemetryClient;

            public Handler(IConfigurationManager<Configurations> configurationManager, ICLIConfigurator cliConfigurator, TelemetryClient telemetryClient)
            {
                _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
                _cliConfigurator = cliConfigurator ?? throw new ArgumentNullException(nameof(cliConfigurator));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                var config = await _configurationManager.LoadAsync(ct: ct);

                if (config.SellerId == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync(
                        "Configure",
                        await _cliConfigurator.ConfigureAsync(false, ct: ct) ? 0 : -1,
                        ct);
                }
                else
                {
                    HelpBuilder helpBuilder = new(LocalizationResources.Instance, CommandExtensions.GetBufferWidth());
                    helpBuilder.Write(context.ParseResult.RootCommandResult.Command, Console.Out);
                    AnsiConsole.MarkupLine("Use of the Microsoft Store Developer CLI is subject to the terms of the Microsoft Privacy Statement: [link]https://aka.ms/privacy[/]");
                }

                return 0;
            }
        }

        internal static async Task<bool> InitAsync(IConfigurationManager<Configurations> configurationManager, ICredentialManager credentialManager, IConsoleReader consoleReader, ICLIConfigurator cliConfigurator, ILogger logger, CancellationToken ct)
        {
            Configurations config;
            try
            {
                config = await configurationManager.LoadAsync(ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Something in the config file seems wrong...");
                return await StartOverAsync(configurationManager, consoleReader, cliConfigurator, ct);
            }

            if (config.SellerId == null)
            {
                logger.LogCritical("SellerId is not set.");
                return false;
            }

            if (!config.ClientId.HasValue)
            {
                AnsiConsole.MarkupLine("Configuration Client Id is empty. Please, run the [b green]reconfigure[/] command");
                return false;
            }

            var secret = credentialManager.ReadCredential(config.ClientId.Value.ToString());
            if (string.IsNullOrEmpty(secret))
            {
                AnsiConsole.MarkupLine("We could not find credentials that match your configurations.");
                logger.LogCritical("Secret is empty");

                return await StartOverAsync(configurationManager, consoleReader, cliConfigurator, ct);
            }

            return true;
        }

        internal static async Task<bool> StartOverAsync(IConfigurationManager<Configurations> configurationManager, IConsoleReader consoleReader, ICLIConfigurator cliConfigurator, CancellationToken ct)
        {
            if (await consoleReader.YesNoConfirmationAsync("Do you want to start over and reset the settings? I'll ask for the credentials all over again, ok?", ct))
            {
                _ = await configurationManager.LoadAsync(true, ct);
                await configurationManager.ClearAsync(ct);
                AnsiConsole.WriteLine("Configuration cleared.");
                return await cliConfigurator.ConfigureAsync(false, ct: ct);
            }
            else
            {
                return false;
            }
        }
    }
}