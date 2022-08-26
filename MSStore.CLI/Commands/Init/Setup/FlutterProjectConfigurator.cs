// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Init.Setup
{
    internal class FlutterProjectConfigurator : IProjectConfigurator
    {
        private readonly IExternalCommandExecutor _externalCommandExecutor;

        public FlutterProjectConfigurator(IExternalCommandExecutor externalCommandExecutor)
        {
            _externalCommandExecutor = externalCommandExecutor ?? throw new ArgumentNullException(nameof(externalCommandExecutor));
        }

        public string ConfiguratorProjectType { get; } = "Flutter";

        public string[] SupportedProjectPattern { get; } = new[] { "pubspec.yaml" };

        public bool CanConfigure(string pathOrUrl)
        {
            if (string.IsNullOrEmpty(pathOrUrl))
            {
                return false;
            }

            try
            {
                DirectoryInfo directoryPath = new DirectoryInfo(pathOrUrl);
                return SupportedProjectPattern.Any(y => directoryPath.GetFiles(y).Any());
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> ConfigureAsync(string pathOrUrl, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            DirectoryInfo projectRootPath = new DirectoryInfo(pathOrUrl);
            var flutterProjectFiles = projectRootPath.GetFiles(SupportedProjectPattern.First(), SearchOption.TopDirectoryOnly);

            if (flutterProjectFiles.Length == 0)
            {
                throw new InvalidOperationException("No pubspec.yaml file found in the project root directory.");
            }

            var flutterProjectFile = flutterProjectFiles.First();

            await InstallMsixDependencyAsync(projectRootPath, ct);

            using var fileStream = flutterProjectFile.Open(FileMode.Open, FileAccess.ReadWrite);

            bool msixConfigExists = false;

            string[] yamlLines;

            using var streamReader = new StreamReader(fileStream);
            using var streamWriter = new StreamWriter(fileStream);

            var yaml = await streamReader.ReadToEndAsync(ct);

            yamlLines = yaml.Split(Environment.NewLine);

            foreach (var line in yamlLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var indexOfMsixConfig = line.IndexOf("msix_config", StringComparison.OrdinalIgnoreCase);
                    if (indexOfMsixConfig > -1)
                    {
                        var commentStartIndex = line.IndexOf('#');
                        if (commentStartIndex == -1 || commentStartIndex > indexOfMsixConfig)
                        {
                            msixConfigExists = true;
                        }
                    }
                }
            }

            if (!msixConfigExists)
            {
                fileStream.Seek(0, SeekOrigin.End);

                if (!string.IsNullOrWhiteSpace(yamlLines.Last()))
                {
                    streamWriter.WriteLine();
                }

                streamWriter.WriteLine();

                streamWriter.WriteLine($"msix_config:");
                streamWriter.WriteLine($"  display_name: {app.PrimaryName}");
                streamWriter.WriteLine($"  publisher_display_name: {publisherDisplayName}");
                streamWriter.WriteLine($"  identity_name: {app.PackageIdentityName}");
                streamWriter.WriteLine($"  msix_version: 0.0.1.0");
                streamWriter.WriteLine($"  logo_path: C:\\path\\to\\logo.png");
                streamWriter.WriteLine($"  capabilities: internetClient");

                AnsiConsole.WriteLine($"Flutter project '{flutterProjectFile.FullName}' is now configured to build to the Microsoft Store!");
                AnsiConsole.WriteLine("For more information on building your Flutter project to the Microsoft Store, see https://pub.dev/packages/msix#microsoft-store-icon-publishing-to-the-microsoft-store");

                return 0;
            }
            else
            {
                AnsiConsole.WriteLine();

                return -1;
            }
        }

        private async Task InstallMsixDependencyAsync(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            var msixInstalled = await AnsiConsole.Status().StartAsync("Checking if package 'msix' is already installed...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("flutter pub add --dev msix --dry-run", projectRootPath.FullName, ct);
                    if ((result.ExitCode == 0 && result.StdOut.Contains("No dependencies would change")) ||
                        (result.ExitCode == 65 && result.StdErr.Contains("\"msix\" is already in \"dev_dependencies\"")))
                    {
                        ctx.SuccessStatus("'msix' package is already installed, no need to install it again.");
                        return true;
                    }

                    ctx.SuccessStatus("'msix' package is not yet installed.");
                    return false;
                }
                catch (Exception)
                {
                    // _logger...
                    throw new MSStoreException("Failed to check if msix package is already installed..");
                }
            });

            if (msixInstalled == true)
            {
                return;
            }

            AnsiConsole.WriteLine();

            await AnsiConsole.Status().StartAsync("Installing 'msix' package...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("flutter pub add --dev msix", projectRootPath.FullName, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    ctx.SuccessStatus("'msix' package installed successfully!");

                    return 0;
                }
                catch (Exception ex)
                {
                    // _logger...
                    throw new InvalidOperationException("Failed to install msix package.", ex);
                }
            });
        }
    }
}
