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
        internal static Option<bool> VerboseOption { get; }

        static MicrosoftStoreCLI()
        {
            VerboseOption = new Option<bool>("--verbose", "-v")
            {
                DefaultValueFactory = _ => false,
                Description = "Verbose output"
            };
        }

        internal static void WelcomeMessage(IAnsiConsole ansiConsole)
        {
            ansiConsole.WriteLine();
            ansiConsole.Write(
                new FigletText("Microsoft Store Dev CLI")
                    .Color(Color.Blue));
            ansiConsole.WriteLine();
        }

        public MicrosoftStoreCLI(InfoCommand infoCommand, ReconfigureCommand reconfigureCommand, SettingsCommand settingsCommand, AppsCommand appsCommand, SubmissionCommand submissionCommand, FlightsCommand flightsCommand, InitCommand initCommand, PackageCommand packageCommand, PublishCommand publishCommand, Handler handler)
            : base(description: "CLI tool to automate Microsoft Store Developer tasks.")
        {
            Subcommands.Add(infoCommand);
            Subcommands.Add(reconfigureCommand);
            Subcommands.Add(settingsCommand);
            Subcommands.Add(appsCommand);
            Subcommands.Add(submissionCommand);
            Subcommands.Add(flightsCommand);
            Subcommands.Add(initCommand);
            Subcommands.Add(packageCommand);
            Subcommands.Add(publishCommand);

            SetAction((parseResult, ct) =>
            {
                foreach (var option in Options)
                {
                    if (option is HelpOption defaultHelpOption && defaultHelpOption.Action is HelpAction helpAction)
                    {
                        helpAction.Invoke(parseResult);
                        return Task.CompletedTask;
                    }
                }

                return handler.InvokeAsync(parseResult, ct);
            });
        }

        public class Handler(IConfigurationManager<Configurations> configurationManager, ICLIConfigurator cliConfigurator, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly IConfigurationManager<Configurations> _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            private readonly ICLIConfigurator _cliConfigurator = cliConfigurator ?? throw new ArgumentNullException(nameof(cliConfigurator));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var config = await _configurationManager.LoadAsync(ct: ct);

                if (config.SellerId == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync(
                        "Configure",
                        await _cliConfigurator.ConfigureAsync(_ansiConsole, false, ct: ct) ? 0 : -1,
                        ct);
                }
                else
                {
                    foreach (var option in parseResult.RootCommandResult.Command.Options)
                    {
                        if (option is HelpOption defaultHelpOption && defaultHelpOption.Action is HelpAction helpAction)
                        {
                            helpAction.Invoke(parseResult);
                            break;
                        }
                    }

                    _ansiConsole.MarkupLine("Use of the Microsoft Store Developer CLI is subject to the terms of the Microsoft Privacy Statement: [link]https://aka.ms/privacy[/]");
                }

                return 0;
            }
        }

        internal static async Task<bool> InitAsync(IAnsiConsole ansiConsole, IConfigurationManager<Configurations> configurationManager, ICredentialManager credentialManager, IConsoleReader consoleReader, ICLIConfigurator cliConfigurator, ILogger logger, CancellationToken ct)
        {
            Configurations config;
            try
            {
                config = await configurationManager.LoadAsync(ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Something in the config file seems wrong...");
                return await StartOverAsync(ansiConsole, configurationManager, consoleReader, cliConfigurator, ct);
            }

            if (config.SellerId == null)
            {
                logger.LogCritical("SellerId is not set.");
                return false;
            }

            if (!config.ClientId.HasValue)
            {
                ansiConsole.MarkupLine("Configuration Client Id is empty. Please, run the [b green]reconfigure[/] command");
                return false;
            }

            var secret = credentialManager.ReadCredential(config.ClientId.Value.ToString());
            if (string.IsNullOrEmpty(config.CertificateFilePath)
                && string.IsNullOrEmpty(config.CertificateThumbprint)
                && string.IsNullOrEmpty(secret))
            {
                ansiConsole.MarkupLine("We could not find credentials that match your configurations.");
                logger.LogCritical("Secret is empty");

                return await StartOverAsync(ansiConsole, configurationManager, consoleReader, cliConfigurator, ct);
            }

            return true;
        }

        internal static async Task<bool> StartOverAsync(IAnsiConsole ansiConsole, IConfigurationManager<Configurations> configurationManager, IConsoleReader consoleReader, ICLIConfigurator cliConfigurator, CancellationToken ct)
        {
            if (await consoleReader.YesNoConfirmationAsync("Do you want to start over and reset the settings? I'll ask for the credentials all over again, ok?", ct))
            {
                _ = await configurationManager.LoadAsync(true, ct);
                await configurationManager.ClearAsync(ct);
                ansiConsole.WriteLine("Configuration cleared.");
                return await cliConfigurator.ConfigureAsync(ansiConsole, false, ct: ct);
            }
            else
            {
                return false;
            }
        }
    }
}