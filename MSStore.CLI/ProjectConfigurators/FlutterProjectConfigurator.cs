// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
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
        public override PublishFileSearchFilterStrategy PublishFileSearchFilterStrategy { get; } = PublishFileSearchFilterStrategy.Newest;
        public override string OutputSubdirectory { get; } = Path.Combine("build", "windows", "MSStore.CLI");
        public override string DefaultInputSubdirectory { get; } = Path.Combine("build", "windows", "runner", "Release");
        public override IEnumerable<BuildArch>? DefaultBuildArchs => new[] { BuildArch.X64 };

        public override bool PackageOnlyOnWindows => true;

        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, Version? version, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, flutterProjectFile) = GetInfo(pathOrUrl);

            await InstallMsixDependencyAsync(projectRootPath, ct);

            var result = await UpdateManifestAsync(projectRootPath, flutterProjectFile, app, publisherDisplayName, version, _imageConverter, _externalCommandExecutor, ct);
            if (result != 0)
            {
                return (result, null);
            }

            AnsiConsole.WriteLine($"Flutter project '{flutterProjectFile.FullName}' is now configured to build to the Microsoft Store!");
            AnsiConsole.MarkupLine("For more information on building your Flutter project to the Microsoft Store, see [link]https://pub.dev/packages/msix#microsoft-store-icon-publishing-to-the-microsoft-store[/]");

            return (0, output);
        }

        internal static async Task<int> UpdateManifestAsync(DirectoryInfo projectRootPath, FileInfo flutterProjectFile, DevCenterApplication app, string publisherDisplayName, Version? version, IImageConverter? imageConverter, IExternalCommandExecutor? externalCommandExecutor, CancellationToken ct)
        {
            using var fileStream = flutterProjectFile.Open(FileMode.Open, FileAccess.ReadWrite);

            int msixConfigLine = -1;

            using var streamReader = new StreamReader(fileStream);
            using var streamWriter = new StreamWriter(fileStream);

            var yaml = await streamReader.ReadToEndAsync(ct);

            var yamlLines = yaml.Split(Environment.NewLine).ToList();

            for (var i = 0; i < yamlLines.Count; i++)
            {
                var line = yamlLines[i];
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var indexOfMsixConfig = line.IndexOf("msix_config", StringComparison.OrdinalIgnoreCase);
                    if (indexOfMsixConfig > -1)
                    {
                        var commentStartIndex = line.IndexOf('#');
                        if (commentStartIndex == -1 || commentStartIndex > indexOfMsixConfig)
                        {
                            msixConfigLine = i;
                            break;
                        }
                    }
                }
            }

            string? imagePath = null;
            if (imageConverter != null && externalCommandExecutor != null)
            {
                imagePath = await GenerateImageFromIcoAsync(projectRootPath, imageConverter, externalCommandExecutor, ct);

                if (imagePath == null)
                {
                    return -2;
                }
            }

            void TryAddOrUpdate(string key, string? value)
            {
                for (var i = msixConfigLine + 1; i < yamlLines.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(yamlLines[i]))
                    {
                        if (yamlLines[i].Contains(key, StringComparison.OrdinalIgnoreCase))
                        {
                            var index = yamlLines[i].IndexOf($"{key}:", StringComparison.OrdinalIgnoreCase);
                            if (index > 0)
                            {
                                yamlLines[i] = $"{yamlLines[i][..index]}{key}: {value}";
                                return;
                            }
                        }
                    }
                }

                // Could not update, so let's add at the beggining of the msix_config section
                yamlLines.Insert(msixConfigLine + 1, $"  {key}: {value}");
            }

            if (!string.IsNullOrWhiteSpace(yamlLines.Last()))
            {
                yamlLines.Add(string.Empty);
            }

            if (msixConfigLine == -1)
            {
                yamlLines.Add("msix_config:");
                msixConfigLine = yamlLines.Count - 1;
            }

            TryAddOrUpdate("msstore_appId", app.Id);
            if (!string.IsNullOrEmpty(imagePath))
            {
                TryAddOrUpdate("logo_path", Path.GetRelativePath(projectRootPath.FullName, imagePath));
            }

            TryAddOrUpdate("msix_version", version != null ? version.ToVersionString() : "0.0.1.0");
            TryAddOrUpdate("identity_name", app.PackageIdentityName);
            TryAddOrUpdate("publisher", app.PublisherName);
            TryAddOrUpdate("publisher_display_name", publisherDisplayName);
            TryAddOrUpdate("display_name", app.PrimaryName);

            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);

            streamWriter.Write(string.Join(Environment.NewLine, yamlLines));

            return 0;
        }

        public override async Task<List<string>?> GetAppImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            var (projectRootPath, flutterProjectFile) = GetInfo(pathOrUrl);

            var images = new List<string>();

            var icoIcon = GetIcoIcon(projectRootPath);
            if (icoIcon != null)
            {
                images.Add(icoIcon);
            }

            var icon = await GetIconAsync(projectRootPath, flutterProjectFile, ct);
            if (icon != null)
            {
                images.Add(icon);
            }

            return images;
        }

        private static async Task<string?> GetIconAsync(DirectoryInfo? projectRootPath, FileInfo? flutterProjectFile, CancellationToken ct)
        {
            if (projectRootPath == null || flutterProjectFile == null)
            {
                return null;
            }

            var logoPath = await GetYamlPropertyAsync(flutterProjectFile, "logo_path", ct);
            if (logoPath == null)
            {
                return null;
            }

            return Path.Combine(projectRootPath.FullName, logoPath);
        }

        public override async Task<List<string>?> GetDefaultImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            List<string> images = new List<string>();

            var flutterDir = await GetFlutterDirAsync(_externalCommandExecutor, ct);
            if (flutterDir == null)
            {
                return images;
            }

            var iconSubPath = Path.Combine("templates", "app_shared", "windows.tmpl", "runner", "resources", "app_icon.ico");
            var iconTemplateSubPath = $"{iconSubPath}.img.tmpl";

            var flutterDefaultAppIconTemplate = Path.Combine(flutterDir, "packages", "flutter_tools", iconTemplateSubPath);
            if (File.Exists(flutterDefaultAppIconTemplate))
            {
                var flutter_template_images = GetPubPackagePath(flutterDir, "flutter_template_images");

                if (flutter_template_images?.FullName != null)
                {
                    var flutterDefaultAppIcon = Path.Combine(flutter_template_images.FullName, iconSubPath);
                    if (File.Exists(flutterDefaultAppIcon))
                    {
                        images.Add(flutterDefaultAppIcon);
                    }
                }
            }

            var msix = GetPubPackagePath(flutterDir, "msix");

            if (msix?.FullName != null)
            {
                var iconsSubPath = Path.Combine(msix.FullName, "lib", "assets", "icons");
                images.AddRange(Directory.GetFiles(iconsSubPath, "*.png", SearchOption.TopDirectoryOnly));
            }

            return images;
        }

        private static async Task<string?> GetFlutterDirAsync(IExternalCommandExecutor externalCommandExecutor, CancellationToken ct)
        {
            var flutterExecutablePath = await externalCommandExecutor.FindToolAsync("flutter", ct);
            if (!string.IsNullOrEmpty(flutterExecutablePath))
            {
                var bin = Directory.GetParent(flutterExecutablePath);
                if (bin != null)
                {
                    var flutterDir = bin.Parent;
                    if (flutterDir != null && flutterDir.FullName != null)
                    {
                        return flutterDir.FullName;
                    }
                }
            }

            return null;
        }

        private static DirectoryInfo? GetPubPackagePath(string flutterDir, string packageName, string version = "*")
        {
            var pubDartlangOrgDir = new DirectoryInfo(Path.Combine(flutterDir, ".pub-cache", "hosted", "pub.dartlang.org"));
            if (pubDartlangOrgDir.Exists)
            {
                return pubDartlangOrgDir
                    .GetDirectories($"{packageName}-{version}", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(d => d.Name)
                    .FirstOrDefault();
            }

            return null;
        }

        private static async Task<string?> GenerateImageFromIcoAsync(DirectoryInfo projectRootPath, IImageConverter imageConverter, IExternalCommandExecutor externalCommandExecutor, CancellationToken ct)
        {
            try
            {
                var icon = GetIcoIcon(projectRootPath);
                if (icon == string.Empty)
                {
                    return string.Empty;
                }

                if (icon == null)
                {
                    return null;
                }

                var flutterDir = await GetFlutterDirAsync(externalCommandExecutor, ct);

                DirectoryInfo? msix = null;
                bool isDefaultIcon = false;
                if (flutterDir != null)
                {
                    msix = GetPubPackagePath(flutterDir, "msix");
                    isDefaultIcon = IsDefaultIcon(flutterDir, icon, imageConverter);
                }

                var logoPath = Path.ChangeExtension(icon, ".png");
                if (flutterDir == null || msix?.FullName == null || !isDefaultIcon)
                {
                    if (!await imageConverter.ConvertIcoToPngAsync(icon, logoPath, 256, 256, 12, 12, ct))
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to convert icon to png.[/]");
                        return null;
                    }
                }
                else
                {
                    var iconsSubPath = Path.Combine(msix.FullName, "lib", "assets", "icons");
                    var file = Directory.GetFiles(iconsSubPath, "StoreLogo.scale-400.png", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (file != null)
                    {
                        File.Copy(file, logoPath, true);
                    }
                }

                return logoPath;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error generating logo.png from icon.ico: {ex.Message}[/]");
                return null;
            }
        }

        private static bool IsDefaultIcon(string flutterDir, string iconPath, IImageConverter imageConverter)
        {
            var flutter_template_images = GetPubPackagePath(flutterDir, "flutter_template_images");

            if (flutter_template_images?.FullName != null)
            {
                var iconSubPath = Path.Combine("templates", "app_shared", "windows.tmpl", "runner", "resources", "app_icon.ico");
                var flutterDefaultAppIcon = Path.Combine(flutter_template_images.FullName, iconSubPath);
                if (File.Exists(flutterDefaultAppIcon))
                {
                    try
                    {
                        var byte1Array = imageConverter.ConvertToByteArray(iconPath);
                        var byte2Array = imageConverter.ConvertToByteArray(flutterDefaultAppIcon);
                        if (byte1Array != null && byte2Array != null)
                        {
                            return SHA512.HashData(byte1Array).SequenceEqual(SHA512.HashData(byte2Array));
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private static string? GetIcoIcon(DirectoryInfo projectRootPath)
        {
            var resourcesDirInfo = new DirectoryInfo(Path.Combine(projectRootPath.FullName, "windows", "runner", "resources"));
            if (!resourcesDirInfo.Exists)
            {
                return string.Empty;
            }

            var icons = resourcesDirInfo.GetFiles("*.ico");
            return icons.FirstOrDefault()?.FullName;
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

        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, Version? version, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, _) = GetInfo(pathOrUrl);

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

                    if (version != null)
                    {
                        args += $" --version {version.ToVersionString()}";
                    }

                    if (buildArchs?.Any() == true)
                    {
                        if (!buildArchs.Contains(BuildArch.X64))
                        {
                            Logger.LogError("Flutter builds to the Microsoft Store only support x64.");
                            return (-3, null);
                        }

                        if (buildArchs.Contains(BuildArch.X86))
                        {
                            Logger.LogWarning("Flutter does not support building for Windows x86. Skipping x86 build. (More info: https://github.com/flutter/flutter/issues/37777)");
                        }

                        if (buildArchs.Contains(BuildArch.Arm64))
                        {
                            Logger.LogWarning("Flutter does not yet support building for Windows ARM64. Skipping ARM64 build. (More info: https://github.com/flutter/flutter/issues/53120)");
                        }
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
                            msixFile = new FileInfo(Path.Combine(projectRootPath.FullName, msixPath));
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

        public override Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct)
        {
            return GetYamlPropertyAsync(fileInfo, "msstore_appId", ct);
        }

        private static async Task<string?> GetYamlPropertyAsync(FileInfo? flutterProjectFile, string property, CancellationToken ct)
        {
            if (flutterProjectFile == null)
            {
                return null;
            }

            string? propertyValue = null;
            using var fileStream = flutterProjectFile.Open(FileMode.Open, FileAccess.ReadWrite);

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
                        var indexOfProperty = line.IndexOf(property, StringComparison.OrdinalIgnoreCase);
                        if (indexOfProperty > -1)
                        {
                            var commentStartIndex = line.IndexOf('#');
                            if (commentStartIndex == -1 || commentStartIndex > indexOfProperty)
                            {
                                if (commentStartIndex > indexOfProperty)
                                {
                                    propertyValue = line.Substring(0, commentStartIndex).Split(':').LastOrDefault()?.Trim();
                                }
                                else
                                {
                                    propertyValue = line.Split(':').LastOrDefault()?.Trim();
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return propertyValue;
        }
    }
}
