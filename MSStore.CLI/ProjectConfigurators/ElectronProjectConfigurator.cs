// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services;
using MSStore.CLI.Services.ElectronManager;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class ElectronProjectConfigurator : FileProjectConfigurator
    {
        private readonly IExternalCommandExecutor _externalCommandExecutor;
        private readonly IElectronManifestManager _electronManifestManager;

        public ElectronProjectConfigurator(IExternalCommandExecutor externalCommandExecutor, IElectronManifestManager electronManifestManager, IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, ILogger<ElectronProjectConfigurator> logger)
            : base(browserLauncher, consoleReader, zipFileManager, fileDownloader, azureBlobManager, logger)
        {
            _externalCommandExecutor = externalCommandExecutor ?? throw new ArgumentNullException(nameof(externalCommandExecutor));
            _electronManifestManager = electronManifestManager ?? throw new ArgumentNullException(nameof(electronManifestManager));
        }

        public override string ConfiguratorProjectType { get; } = "Electron";

        public override string[] SupportedProjectPattern { get; } = new[] { "package.json" };

        public override string[] PackageFilesExtensionInclude => new[] { ".appx" };
        public override string[]? PackageFilesExtensionExclude { get; }
        public override SearchOption PackageFilesSearchOption { get; } = SearchOption.TopDirectoryOnly;
        public override PublishFileSearchFilterStrategy PublishFileSearchFilterStrategy { get; } = PublishFileSearchFilterStrategy.All;
        public override string OutputSubdirectory { get; } = "dist";
        public override string DefaultInputSubdirectory { get; } = "dist";
        public override IEnumerable<BuildArch>? DefaultBuildArchs => new[] { BuildArch.X64, BuildArch.Arm64 };

        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, electronProjectFile) = GetInfo(pathOrUrl);

            if (!await InstallElectronBuilderDependencyAsync(projectRootPath, ct))
            {
                return (-1, null);
            }

            var electronManifest = await _electronManifestManager.LoadAsync(electronProjectFile, ct);

            electronManifest.Build ??= new ElectronManifestBuild();
            electronManifest.Build.Windows ??= new ElectronManifestBuildWindows();
            if (electronManifest.Build.Windows.Targets is JsonArray targets)
            {
                if (targets.All(t => t?.ToString() != "appx"))
                {
                    targets.Add("appx");
                }
            }
            else if (electronManifest.Build.Windows.Targets is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue(out string? existingTarget))
                {
                    electronManifest.Build.Windows.Targets = new JsonArray { "appx", existingTarget };
                }
            }
            else
            {
                electronManifest.Build.Windows.Targets = new JsonArray { "appx" };
            }

            electronManifest.Build.Appx ??= new ElectronManifestBuildAppX();
            electronManifest.Build.Appx.PublisherDisplayName = publisherDisplayName;
            electronManifest.Build.Appx.DisplayName = app.PrimaryName;
            electronManifest.Build.Appx.Publisher = app.PublisherName;
            electronManifest.Build.Appx.IdentityName = app.PackageIdentityName;
            electronManifest.Build.Appx.ApplicationId = "App";
            electronManifest.MSStoreCLIAppID = app.Id;

            await _electronManifestManager.SaveAsync(electronManifest, electronProjectFile, ct);

            AnsiConsole.WriteLine($"Electron project '{electronProjectFile.FullName}' is now configured to build to the Microsoft Store!");
            AnsiConsole.MarkupLine("For more information on building your Electron project to the Microsoft Store, see [link]https://www.electron.build/configuration/appx#how-to-publish-your-electron-app-to-the-windows-app-store[/]");

            return (0, output);
        }

        private static bool IsYarn(DirectoryInfo projectRootPath)
        {
            return projectRootPath.GetFiles("yarn.lock", SearchOption.TopDirectoryOnly).Any();
        }

        private async Task<bool> RunNpmInstall(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            return await AnsiConsole.Status().StartAsync("Running 'npm install'...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("npm", "install", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0)
                    {
                        ctx.SuccessStatus("'npm install' ran successfully.");
                        return true;
                    }

                    ctx.ErrorStatus("'npm install' failed.");

                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'npm install'.");
                    throw new MSStoreException("Failed to run 'npm install'.");
                }
            });
        }

        private async Task<bool> RunYarnInstall(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            return await AnsiConsole.Status().StartAsync("Running 'yarn install'...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("yarn", "install", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0)
                    {
                        ctx.SuccessStatus("'yarn install' ran successfully.");
                        return true;
                    }

                    ctx.ErrorStatus("'yarn install' failed.");

                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'yarn install'.");
                    throw new MSStoreException("Failed to run 'yarn install'.");
                }
            });
        }

        private Task<bool> InstallElectronBuilderDependencyAsync(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            if (IsYarn(projectRootPath))
            {
                return InstallElectronBuilderYarnDependencyAsync(projectRootPath, ct);
            }
            else
            {
                return InstallElectronBuilderNpmDependencyAsync(projectRootPath, ct);
            }
        }

        private async Task<bool> InstallElectronBuilderNpmDependencyAsync(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            var npmInstall = await RunNpmInstall(projectRootPath, ct);

            if (!npmInstall)
            {
                throw new MSStoreException("Failed to run 'npm install'.");
            }

            var electronBuilderInstalled = await AnsiConsole.Status().StartAsync("Checking if package 'electron-builder' is already installed...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("npm", "list electron-builder", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0 && result.StdOut.Contains("`-- electron-builder@"))
                    {
                        ctx.SuccessStatus("'electron-builder' package is already installed, no need to install it again.");
                        return true;
                    }

                    ctx.SuccessStatus("'electron-builder' package is not yet installed.");
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'npm list electron-builder'.");
                    throw new MSStoreException("Failed to check if 'electron-builder' package is already installed..");
                }
            });

            if (electronBuilderInstalled)
            {
                return true;
            }

            AnsiConsole.WriteLine();

            return await AnsiConsole.Status().StartAsync("Installing 'electron-builder' package...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("npm", "install --save-dev electron-builder", projectRootPath.FullName, ct);
                    if (result.ExitCode != 0)
                    {
                        ctx.ErrorStatus("'npm install --save-dev electron-builder' failed.");
                        return false;
                    }

                    ctx.SuccessStatus("'electron-builder' package installed successfully!");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'npm install --save-dev electron-builder'.");
                    throw new MSStoreException("Failed to install 'electron-builder' package.");
                }
            });
        }

        private async Task<bool> InstallElectronBuilderYarnDependencyAsync(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            var yarnInstall = await RunYarnInstall(projectRootPath, ct);

            if (!yarnInstall)
            {
                throw new MSStoreException("Failed to run 'yarn install'.");
            }

            var electronBuilderInstalled = await AnsiConsole.Status().StartAsync("Checking if package 'electron-builder' is already installed...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("yarn", "list --pattern electron-builder", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0 && result.StdOut.Contains("electron-builder@"))
                    {
                        ctx.SuccessStatus("'electron-builder' package is already installed, no need to install it again.");
                        return true;
                    }

                    ctx.SuccessStatus("'electron-builder' package is not yet installed.");
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'yarn list electron-builder'.");
                    throw new MSStoreException("Failed to check if 'electron-builder' package is already installed..");
                }
            });

            if (electronBuilderInstalled)
            {
                return true;
            }

            AnsiConsole.WriteLine();

            return await AnsiConsole.Status().StartAsync("Installing 'electron-builder' package...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync("yarn", "add --dev electron-builder", projectRootPath.FullName, ct);
                    if (result.ExitCode != 0)
                    {
                        ctx.ErrorStatus("'yarn add --dev electron-builder' failed.");
                        return false;
                    }

                    ctx.SuccessStatus("'electron-builder' package installed successfully!");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'yarn add --dev electron-builder'.");
                    throw new MSStoreException("Failed to install 'electron-builder' package.");
                }
            });
        }

        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, electronProjectFile) = GetInfo(pathOrUrl);

            bool isYarn = IsYarn(projectRootPath);

            if (app == null)
            {
                if (isYarn)
                {
                    var yarnInstall = await RunYarnInstall(projectRootPath, ct);

                    if (!yarnInstall)
                    {
                        throw new MSStoreException("Failed to run 'yarn install'.");
                    }
                }
                else
                {
                    var npmInstall = await RunNpmInstall(projectRootPath, ct);

                    if (!npmInstall)
                    {
                        throw new MSStoreException("Failed to run 'npm install'.");
                    }
                }
            }

            return await AnsiConsole.Status().StartAsync("Packaging 'msix'...", async ctx =>
            {
                try
                {
                    var args = "-w=appx";
                    if (output != null)
                    {
                        /*
                        // Not Supported yet
                        // args += $" --output-path \"{output.FullName}\"";
                        */
                    }

                    if (buildArchs?.Any() == true)
                    {
                        if (buildArchs.Contains(BuildArch.X64))
                        {
                            args += " --x64";
                        }

                        if (buildArchs.Contains(BuildArch.X86))
                        {
                            args += " --ia32";
                        }

                        if (buildArchs.Contains(BuildArch.Arm64))
                        {
                            args += " --arm64";
                        }
                    }

                    string command, arguments;
                    if (isYarn)
                    {
                        command = "yarn";
                        arguments = $"run electron-builder build {args}";
                    }
                    else
                    {
                        command = "npx";
                        arguments = $"electron-builder build {args}";
                    }

                    var result = await _externalCommandExecutor.RunAsync(command, arguments, projectRootPath.FullName, ct);

                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    ctx.SuccessStatus("Store package built successfully!");

                    var cleanedStdOut = System.Text.RegularExpressions.Regex.Replace(result.StdOut, @"\e([^\[\]]|\[.*?[a-zA-Z]|\].*?\a)", string.Empty);

                    var msixLine = cleanedStdOut.Split(new string[] { "\n", Environment.NewLine }, StringSplitOptions.None).LastOrDefault(line => line.Contains("target=AppX"));
                    int index;
                    var search = "file=";
                    if (msixLine == null || (index = msixLine.IndexOf(search, StringComparison.OrdinalIgnoreCase)) == -1)
                    {
                        throw new MSStoreException("Failed to find the path to the packaged msix file.");
                    }

                    var msixPath = msixLine.Substring(index + search.Length).Trim();

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

            var electronManifest = await _electronManifestManager.LoadAsync(fileInfo, ct);

            return electronManifest.MSStoreCLIAppID;
        }
    }
}
