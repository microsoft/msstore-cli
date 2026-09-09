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
        internal static readonly Argument<string> KeyArgument;
        internal static readonly Option<string> RegionOption;
        internal static readonly Option<bool> ClearOption;

        static SetTranslatorKeyCommand()
        {
            KeyArgument = new Argument<string>("key")
            {
                Description = "The Azure AI Translator resource key. Used by the --translate option of the reviews commands.",
                Arity = ArgumentArity.ZeroOrOne
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
            : base("set-translator-key", "Store the Azure AI Translator key used by the reviews '--translate' option.")
        {
            Arguments.Add(KeyArgument);
            Options.Add(RegionOption);
            Options.Add(ClearOption);
        }

        public class Handler(
            ILogger<SetTranslatorKeyCommand.Handler> logger,
            ICredentialManager credentialManager,
            IConfigurationManager<Configurations> configurationManager,
            IAnsiConsole ansiConsole,
            TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly ICredentialManager _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
            private readonly IConfigurationManager<Configurations> _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var key = parseResult.GetValue(KeyArgument);
                var region = parseResult.GetValue(RegionOption);
                var clear = parseResult.GetValue(ClearOption);

                try
                {
                    var config = await _configurationManager.LoadAsync(ct: ct);

                    if (clear)
                    {
                        _credentialManager.ClearCredentials(AzureAITranslatorService.CredentialKeyName);
                        config.TranslatorRegion = null;
                        await _configurationManager.SaveAsync(config, ct);

                        _ansiConsole.MarkupLine("Azure AI Translator key and region [bold green]cleared[/].");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                    }

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        _ansiConsole.MarkupLine("[bold red]A key is required.[/] Pass the Azure AI Translator resource key, or use [bold]--clear[/] to remove the stored one.");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                    }

                    // Keys and regions are frequently pasted or piped in with surrounding
                    // whitespace, which is not valid in a request header.
                    _credentialManager.WriteCredential(AzureAITranslatorService.CredentialKeyName, key.Trim());

                    if (!string.IsNullOrWhiteSpace(region))
                    {
                        config.TranslatorRegion = region.Trim();
                        await _configurationManager.SaveAsync(config, ct);
                    }

                    _ansiConsole.MarkupLine("Azure AI Translator key [bold green]stored[/].");

                    if (string.IsNullOrWhiteSpace(region) && string.IsNullOrWhiteSpace(config.TranslatorRegion))
                    {
                        _ansiConsole.MarkupLine("No region is set. That is correct for a [bold]global[/] Translator resource, but regional and multi-service resources need one - re-run with [bold]--region[/].");
                    }

                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while storing the Azure AI Translator key.");
                    _ansiConsole.MarkupLine("[bold red]Could not store the Azure AI Translator key.[/]");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
                }
            }
        }
    }
}
