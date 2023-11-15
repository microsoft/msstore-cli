// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.CLI.ProjectConfigurators;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal static class IProjectConfiguratorExtensions
    {
        internal static async Task ValidateImagesAsync(this IProjectConfigurator configurator, string pathOrUrl, IImageConverter imageConverter, ILogger logger, CancellationToken ct)
        {
            var appImages = await configurator.GetAppImagesAsync(pathOrUrl, ct);
            if (appImages?.Count > 0)
            {
                var projectSpecificDefaultImages = await configurator.GetDefaultImagesAsync(pathOrUrl, ct);
                var defaultImages = ProjectImagesHelper.GetDefaultImagesUsedByApp(appImages, projectSpecificDefaultImages, imageConverter, logger);
                if (defaultImages.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[bold yellow]The following images are using the default values and should be updated:[/]");
                    foreach (var image in defaultImages)
                    {
                        AnsiConsole.MarkupLine($"[bold yellow]  {image}[/]");
                    }
                }
            }
        }
    }
}
