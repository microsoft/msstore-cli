// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace MSStore.CLI.Services
{
    internal class BrowserLauncher(IConsoleReader consoleReader, ILogger<BrowserLauncher> logger, IEnvironmentInformationService environmentInformationService) : IBrowserLauncher
    {
        private readonly IEnvironmentInformationService _environmentInformationService = environmentInformationService ?? throw new System.ArgumentNullException(nameof(environmentInformationService));

        public async Task OpenBrowserAsync(string url, bool askConfirmation, CancellationToken ct)
        {
            if (_environmentInformationService.IsRunningOnCI)
            {
                logger.LogInformation("Skipping browser launch because we are running on CI. URL = {Url}", url);

                return;
            }

            if (askConfirmation)
            {
                AnsiConsole.MarkupLine($"Press [b green]Enter[/] to open the browser at this page: [link]{url.EscapeMarkup()}[/]");

                if (await consoleReader.ReadNextAsync(false, ct) != string.Empty)
                {
                    return;
                }
            }

            logger.LogInformation("Trying to open browser with url: {Url}", url);

            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start(new ProcessStartInfo("xdg-open", url) { RedirectStandardOutput = true, RedirectStandardError = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(new ProcessStartInfo("open", url) { RedirectStandardOutput = true, RedirectStandardError = true });
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
