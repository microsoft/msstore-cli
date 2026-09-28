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
using MSStore.CLI.Services.CredentialManager;
using MSStore.CLI.Services.Translation;
using Spectre.Console;

namespace MSStore.CLI.Commands.Settings
{
    internal class SetTranslatorKeyCommand : Command
    {
        internal static readonly Option<bool> KeyFromStandardInputOption;
        internal static readonly Option<string> RegionOption;
        internal static readonly Option<bool> ClearOption;

        static SetTranslatorKeyCommand()
        {
            // The key is deliberately not accepted as an argument or option value: anything on
            // the command line is recorded in shell history and visible in process listings.
            KeyFromStandardInputOption = new Option<bool>("--key-stdin")
            {
                DefaultValueFactory = _ => false,
                Description = "Read the key from standard input instead of prompting for it, for example when piping it from a secret store."
            };

            RegionOption = new Option<string>("--region", "-r")
            {
                Description = "The region of the Azure AI Translator resource. Required for regional and multi-service resources, and unnecessary for a global one."
            };

            ClearOption = new Option<bool>("--clear")
            {
                DefaultValueFactory = _ => false,
                Description = "Remove the stored Azure AI Translator key and region."
            };
        }

        public SetTranslatorKeyCommand()
            : base("set-translator-key", "Store the Azure AI Translator key used by the reviews '--translate' option. Prompts for the key without echoing it, or reads it from standard input with --key-stdin.")
        {
            Options.Add(KeyFromStandardInputOption);
            Options.Add(RegionOption);
            Options.Add(ClearOption);
        }

        public class Handler(
            ILogger<SetTranslatorKeyCommand.Handler> logger,
            ICredentialManager credentialManager,
            IConfigurationManager<Configurations> configurationManager,
            IConsoleReader consoleReader,
            IEnvironmentInformationService environmentInformationService,
            IAnsiConsole ansiConsole,
            TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly ICredentialManager _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
            private readonly IConfigurationManager<Configurations> _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            private readonly IEnvironmentInformationService _environmentInformationService = environmentInformationService ?? throw new ArgumentNullException(nameof(environmentInformationService));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                if (parseResult.GetValue(ClearOption))
                {
                    return await ClearAsync(ct);
                }

                return await StoreAsync(parseResult.GetValue(KeyFromStandardInputOption), parseResult.GetValue(RegionOption), ct);
            }

            private async Task<int> ClearAsync(CancellationToken ct)
            {
                var keyRemoved = false;

                try
                {
                    // ClearCredentials is best-effort on every platform, so the removal is confirmed
                    // rather than assumed: reporting success while the key survives would leave it in
                    // use. The key goes first, so a failure to remove it changes nothing else.
                    _credentialManager.ClearCredentials(AzureAITranslatorService.CredentialKeyName);

                    if (!string.IsNullOrEmpty(_credentialManager.ReadCredential(AzureAITranslatorService.CredentialKeyName)))
                    {
                        _ansiConsole.MarkupLine("[bold red]The Azure AI Translator key could not be removed from the credential store.[/] Remove it manually.");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                    }

                    keyRemoved = true;

                    var config = await _configurationManager.LoadAsync(ct: ct);
                    config.TranslatorRegion = null;
                    await _configurationManager.SaveAsync(config, ct);

                    _ansiConsole.MarkupLine("Azure AI Translator key and region [bold green]cleared[/].");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while clearing the Azure AI Translator key.");
                    _ansiConsole.MarkupLine(keyRemoved
                        ? "[bold red]The Azure AI Translator key was removed, but the region could not be cleared from settings.json.[/]"
                        : "[bold red]Could not clear the Azure AI Translator key.[/]");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                }
            }

            private async Task<int> StoreAsync(bool keyFromStandardInput, string? region, CancellationToken ct)
            {
                var regionSaved = false;

                try
                {
                    // Loaded before asking for the key, so a broken settings file fails the command
                    // before the user has typed a secret.
                    var config = await _configurationManager.LoadAsync(ct: ct);

                    string? key;
                    if (keyFromStandardInput)
                    {
                        if (!_consoleReader.IsInputRedirected)
                        {
                            _ansiConsole.MarkupLine("[bold red]--key-stdin reads the key from a pipe, but standard input is not redirected.[/] Pipe the key in, or leave out [bold]--key-stdin[/] to be prompted for it.");
                            return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                        }

                        // The user has stated a key is coming, so there is no deadline on it: a
                        // producer such as a secret store lookup can take a while to write.
                        key = await _consoleReader.ReadAllStandardInputAsync(null, ct);
                    }
                    else if (_consoleReader.IsInputRedirected || _environmentInformationService.IsRunningOnCI)
                    {
                        _ansiConsole.MarkupLine($"[bold red]Cannot prompt for the key in a non-interactive session.[/] Pipe it in with [bold]--key-stdin[/], or set the [bold]{AzureAITranslatorService.KeyEnvironmentVariable}[/] environment variable instead of storing a key.");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                    }
                    else
                    {
                        key = await _consoleReader.RequestStringAsync("Azure AI Translator key", true, ct);
                    }

                    // Keys and regions are frequently pasted or piped in with surrounding
                    // whitespace, which is not valid in a request header.
                    key = key?.Trim();

                    if (string.IsNullOrEmpty(key))
                    {
                        _ansiConsole.MarkupLine("[bold red]A key is required.[/] Provide the Azure AI Translator resource key, or use [bold]--clear[/] to remove the stored one.");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                    }

                    // settings.json is saved before the credential is overwritten, following the
                    // reconfigure flow, so a failure saving it leaves the stored key untouched. If
                    // writing the key then fails, the region is put back, so a command that fails
                    // leaves the configuration as it found it.
                    var previousRegion = config.TranslatorRegion;

                    if (!string.IsNullOrWhiteSpace(region))
                    {
                        config.TranslatorRegion = region.Trim();
                        await _configurationManager.SaveAsync(config, ct);
                        regionSaved = true;
                    }

                    try
                    {
                        _credentialManager.WriteCredential(AzureAITranslatorService.CredentialKeyName, key);
                    }
                    catch (Exception writeError) when (regionSaved)
                    {
                        _logger.LogError(writeError, "Error while storing the Azure AI Translator key.");

                        config.TranslatorRegion = previousRegion;
                        await _configurationManager.SaveAsync(config, ct);

                        _ansiConsole.MarkupLine("[bold red]Could not store the Azure AI Translator key.[/]");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                    }

                    _ansiConsole.MarkupLine("Azure AI Translator key [bold green]stored[/].");

                    if (string.IsNullOrWhiteSpace(config.TranslatorRegion))
                    {
                        _ansiConsole.MarkupLine("No region is set. That is correct for a [bold]global[/] Translator resource, but regional and multi-service resources need one - re-run with [bold]--region[/].");
                    }
                    else if (string.IsNullOrWhiteSpace(region))
                    {
                        // A new key may belong to a different resource, so keeping the earlier
                        // region is said out loud rather than done silently.
                        _ansiConsole.MarkupLine($"Using the region '{config.TranslatorRegion.EscapeMarkup()}' stored earlier. Pass [bold]--region[/] to change it, or run [bold]--clear[/] first if the new key is for a global resource.");
                    }

                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while storing the Azure AI Translator key.");
                    _ansiConsole.MarkupLine(regionSaved
                        ? "[bold red]The region was saved, but the Azure AI Translator key could not be stored.[/]"
                        : "[bold red]Could not store the Azure AI Translator key.[/]");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                }
            }
        }
    }
}
