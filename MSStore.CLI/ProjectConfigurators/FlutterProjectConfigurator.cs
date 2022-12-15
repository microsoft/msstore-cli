// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class FlutterProjectConfigurator : FileProjectConfigurator
    {
        private readonly IExternalCommandExecutor _externalCommandExecutor;
        private readonly IImageConverter _imageConverter;

        public FlutterProjectConfigurator(IExternalCommandExecutor externalCommandExecutor, IImageConverter imageConverter, IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, ILogger<FlutterProjectConfigurator> logger)
            : base(browserLauncher, consoleReader, zipFileManager, fileDownloader, azureBlobManager, logger)
        {
            _externalCommandExecutor = externalCommandExecutor ?? throw new ArgumentNullException(nameof(externalCommandExecutor));
            _imageConverter = imageConverter ?? throw new ArgumentNullException(nameof(imageConverter));
        }

        public override string ConfiguratorProjectType { get; } = "Flutter";

        public override string[] SupportedProjectPattern { get; } = new[] { "pubspec.yaml" };

        public override string[] PackageFilesExtensionInclude => new[] { ".msix" };
        public override string[]? PackageFilesExtensionExclude { get; }
        public override SearchOption PackageFilesSearchOption { get; } = SearchOption.TopDirectoryOnly;
        public override string OutputSubdirectory { get; } = Path.Join("build", "windows", "MSStore.CLI");
        public override string DefaultInputSubdirectory { get; } = Path.Combine("build", "windows", "runner", "Release");

        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, flutterProjectFile) = GetInfo(pathOrUrl);

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
                            break;
                        }
                    }
                }
            }

            // TODO: This only works for first initialization.
            // If msix_config section already exists, it won't work.
            if (!msixConfigExists)
            {
                var imagePath = await GenerateImageFromIcoAsync(projectRootPath, ct);

                if (imagePath == null)
                {
                    return (-2, null);
                }

                fileStream.Seek(0, SeekOrigin.End);

                if (!string.IsNullOrWhiteSpace(yamlLines.Last()))
                {
                    streamWriter.WriteLine();
                }

                streamWriter.WriteLine();

                streamWriter.WriteLine($"msix_config:");
                streamWriter.WriteLine($"  display_name: {app.PrimaryName}");
                streamWriter.WriteLine($"  publisher_display_name: {publisherDisplayName}");
                streamWriter.WriteLine($"  publisher: {app.PublisherName}");
                streamWriter.WriteLine($"  identity_name: {app.PackageIdentityName}");
                streamWriter.WriteLine($"  msix_version: 0.0.1.0");
                if (!string.IsNullOrEmpty(imagePath))
                {
                    streamWriter.WriteLine($"  logo_path: {Path.GetRelativePath(projectRootPath.FullName, imagePath)}");
                }

                streamWriter.WriteLine($"  msstore_appId: {app.Id}");

                AnsiConsole.WriteLine($"Flutter project '{flutterProjectFile.FullName}' is now configured to build to the Microsoft Store!");
                AnsiConsole.MarkupLine("For more information on building your Flutter project to the Microsoft Store, see [link]https://pub.dev/packages/msix#microsoft-store-icon-publishing-to-the-microsoft-store[/]");
            }

            return (0, output);
        }

        private async Task<string?> GenerateImageFromIcoAsync(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            try
            {
                var resourcesDirInfo = new DirectoryInfo(Path.Combine(projectRootPath.FullName, "windows", "runner", "resources"));
                if (!resourcesDirInfo.Exists)
                {
                    return string.Empty;
                }

                var icons = resourcesDirInfo.GetFiles("*.ico");
                var icon = icons.FirstOrDefault();
                if (icon == null)
                {
                    return null;
                }

                var logoPath = Path.Combine(resourcesDirInfo.FullName, Path.GetFileNameWithoutExtension(icon.Name) + ".png");

                if (!await _imageConverter.ConvertIcoToPngAsync(icon.FullName, logoPath, ct))
                {
                    AnsiConsole.MarkupLine($"[red]Failed to convert icon to png.[/]");
                    return null;
                }

                return logoPath;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error generating logo.png from icon.ico: {ex.Message}[/]");
                return null;
            }
        }

        private async Task InstallMsixDependencyAsync(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            var pubGet = await RunPubGet(projectRootPath, ct);

            if (!pubGet)
            {
                throw new MSStoreException("Failed to run 'flutter pub get'.");
            }

            var msixInstalled = await AnsiConsole.Status().StartAsync("Checking if package 'msix' is already installed...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("flutter", "pub add --dev msix --dry-run", projectRootPath.FullName, ct);
                    if ((result.ExitCode == 0 && result.StdOut.Contains("No dependencies would change")) ||
                        (result.ExitCode == 65 && result.StdErr.Contains("\"msix\" is already in \"dev_dependencies\"")))
                    {
                        ctx.SuccessStatus("'msix' package is already installed, no need to install it again.");
                        return true;
                    }

                    ctx.SuccessStatus("'msix' package is not yet installed.");
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to check if 'msix' package is installed.");
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
                    var result = await _externalCommandExecutor.RunAsync("flutter", "pub add --dev msix", projectRootPath.FullName, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    ctx.SuccessStatus("'msix' package installed successfully!");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to install 'msix' package.");
                    throw new MSStoreException("Failed to install msix package.");
                }
            });
        }

        private async Task<bool> RunPubGet(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            return await AnsiConsole.Status().StartAsync("Running 'flutter pub get'...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("flutter", "pub get", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0)
                    {
                        ctx.SuccessStatus("'flutter pub get' ran successfully.");
                        return true;
                    }

                    ctx.ErrorStatus("'flutter pub get' failed.");

                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to run 'flutter pub get'.");
                    throw new MSStoreException("Failed to run 'flutter pub get'.");
                }
            });
        }

        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, flutterProjectFile) = GetInfo(pathOrUrl);

            if (app == null)
            {
                var pubGet = await RunPubGet(projectRootPath, ct);

                if (!pubGet)
                {
                    throw new MSStoreException("Failed to run 'flutter pub get'.");
                }
            }

            return await AnsiConsole.Status().StartAsync("Packaging 'msix'...", async ctx =>
            {
                try
                {
                    var args = "--store";
                    if (output != null)
                    {
                        args += $" --output-path \"{output.FullName}\"";
                    }

                    var result = await _externalCommandExecutor.RunAsync("flutter", $"pub run msix:build {args}", projectRootPath.FullName, ct);

                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    ctx.SuccessStatus("Store package built successfully!");

                    result = await _externalCommandExecutor.RunAsync("flutter", $"pub run msix:pack {args}", projectRootPath.FullName, ct);

                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    var cleanedStdOut = System.Text.RegularExpressions.Regex.Replace(result.StdOut, @"\e([^\[\]]|\[.*?[a-zA-Z]|\].*?\a)", string.Empty);

                    var msixLine = cleanedStdOut.Split(new string[] { "\n", Environment.NewLine }, StringSplitOptions.None).LastOrDefault(line => line.Contains("msix created:"));
                    int index;
                    if (msixLine == null || (index = msixLine.IndexOf(": ", StringComparison.OrdinalIgnoreCase)) == -1)
                    {
                        throw new MSStoreException("Failed to find the path to the packaged msix file.");
                    }

                    var msixPath = msixLine.Substring(index + 1).Trim();

                    FileInfo? msixFile = null;
                    if (msixPath != null)
                    {
                        if (Path.IsPathFullyQualified(msixPath))
                        {
                            msixFile = new FileInfo(msixPath);
                        }
                        else
                        {
                            msixFile = new FileInfo(Path.Join(projectRootPath.FullName, msixPath));
                        }
                    }

                    return (0, msixFile?.Directory);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to package 'msix'.");
                    throw new MSStoreException("Failed to generate msix package.", ex);
                }
            });
        }

        public override async Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct)
        {
            if (fileInfo == null)
            {
                return null;
            }

            string? appId = null;
            using var fileStream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite);

            string[] yamlLines;

            using var streamReader = new StreamReader(fileStream);

            var yaml = await streamReader.ReadToEndAsync(ct);

            yamlLines = yaml.Split(Environment.NewLine);

            foreach (var line in yamlLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    try
                    {
                        var indexOfMsstoreAppId = line.IndexOf("msstore_appId", StringComparison.OrdinalIgnoreCase);
                        if (indexOfMsstoreAppId > -1)
                        {
                            var commentStartIndex = line.IndexOf('#');
                            if (commentStartIndex == -1 || commentStartIndex > indexOfMsstoreAppId)
                            {
                                if (commentStartIndex > indexOfMsstoreAppId)
                                {
                                    appId = line.Substring(0, commentStartIndex).Split(':').LastOrDefault()?.Trim();
                                }
                                else
                                {
                                    appId = line.Split(':').LastOrDefault()?.Trim();
                                }

                                if (string.IsNullOrEmpty(appId))
                                {
                                    throw new MSStoreException("Failed to find the 'msstore_appId' in the pubspec.yaml file.");
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return appId;
        }
    }
}
