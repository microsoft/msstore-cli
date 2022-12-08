// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using MSStore.CLI.Services.ElectronManager;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class ElectronProjectConfigurator : IProjectConfigurator, IProjectPackager, IProjectPublisher
    {
        private readonly IExternalCommandExecutor _externalCommandExecutor;
        private readonly IElectronManifestManager _electronManifestManager;
        private readonly IBrowserLauncher _browserLauncher;
        private readonly IConsoleReader _consoleReader;
        private readonly IZipFileManager _zipFileManager;
        private readonly IFileDownloader _fileDownloader;
        private readonly IAzureBlobManager _azureBlobManager;
        private readonly ILogger _logger;

        public ElectronProjectConfigurator(IExternalCommandExecutor externalCommandExecutor, IElectronManifestManager electronManifestManager, IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, ILogger<ElectronProjectConfigurator> logger)
        {
            _externalCommandExecutor = externalCommandExecutor ?? throw new ArgumentNullException(nameof(externalCommandExecutor));
            _electronManifestManager = electronManifestManager ?? throw new ArgumentNullException(nameof(electronManifestManager));
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            _zipFileManager = zipFileManager ?? throw new ArgumentNullException(nameof(zipFileManager));
            _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
            _azureBlobManager = azureBlobManager ?? throw new ArgumentNullException(nameof(azureBlobManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ConfiguratorProjectType { get; } = "Electron";

        public string[] SupportedProjectPattern { get; } = new[] { "package.json" };

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

        private (DirectoryInfo projectRootPath, FileInfo electronProjectFiles) GetInfo(string pathOrUrl)
        {
            DirectoryInfo projectRootPath = new DirectoryInfo(pathOrUrl);
            FileInfo[] electronProjectFiles = projectRootPath.GetFiles(SupportedProjectPattern.First(), SearchOption.TopDirectoryOnly);

            if (electronProjectFiles.Length == 0)
            {
                throw new MSStoreException("No package.json file found in the project root directory.");
            }

            var electronProjectFile = electronProjectFiles.First();

            return (projectRootPath, electronProjectFile);
        }

        public int? ValidateCommand(string pathOrUrl, DirectoryInfo? output, bool? commandPackage, bool? commandPublish)
        {
            return null;
        }

        public async Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, electronProjectFile) = GetInfo(pathOrUrl);

            if (!await InstallElectronBuilderDependencyAsync(projectRootPath, ct))
            {
                return (-1, null);
            }

            var electronManifest = await _electronManifestManager.LoadAsync(electronProjectFile, ct);

            electronManifest.Build ??= new ElectronManifestBuild();
            electronManifest.Build.Windows ??= new ElectronManifestBuildWindows();
            electronManifest.Build.Windows.Targets ??= new List<string>();
            if (!electronManifest.Build.Windows.Targets.Contains("appx"))
            {
                electronManifest.Build.Windows.Targets.Add("appx");
            }

            electronManifest.Build.Appx ??= new ElectronManifestBuildAppX();
            electronManifest.Build.Appx.PublisherDisplayName = publisherDisplayName;
            electronManifest.Build.Appx.DisplayName = app.PrimaryName;
            electronManifest.Build.Appx.Publisher = app.PublisherName;
            electronManifest.Build.Appx.IdentityName = app.PackageIdentityName;
            electronManifest.Build.Appx.ApplicationId = "App";
            electronManifest.MSStoreCLIAppID = app.Id;

            await _electronManifestManager.SaveAsync(electronManifest, electronProjectFile, ct);

            return (0, output);
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
                    _logger.LogError(ex, "Error running 'npm install'.");
                    throw new MSStoreException("Failed to run 'npm install'.");
                }
            });
        }

        private async Task<bool> InstallElectronBuilderDependencyAsync(DirectoryInfo projectRootPath, CancellationToken ct)
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
                    _logger.LogError(ex, "Error running 'npm list electron-builder'.");
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
                    _logger.LogError(ex, "Error running 'npm install --save-dev electron-builder'.");
                    throw new MSStoreException("Failed to install 'electron-builder' package.");
                }
            });
        }

        public async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, electronProjectFile) = GetInfo(pathOrUrl);

            if (app == null)
            {
                var npmInstall = await RunNpmInstall(projectRootPath, ct);

                if (!npmInstall)
                {
                    throw new MSStoreException("Failed to run 'npm install'.");
                }
            }

            return await AnsiConsole.Status().StartAsync("Packaging 'msix'...", async ctx =>
            {
                try
                {
                    var args = "-w";
                    if (output != null)
                    {
                        /*
                        // Not Supported yet
                        // args += $" --output-path \"{output.FullName}\"";
                        */
                    }

                    var result = await _externalCommandExecutor.RunAsync("npx", $"electron-builder build {args}", projectRootPath.FullName, ct);

                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    ctx.SuccessStatus("Store package built successfully!");

                    var cleanedStdOut = System.Text.RegularExpressions.Regex.Replace(result.StdOut, @"\e([^\[\]]|\[.*?[a-zA-Z]|\].*?\a)", string.Empty);

                    var msixLine = cleanedStdOut.Split(new string[] { "\n", Environment.NewLine }, StringSplitOptions.None).LastOrDefault(line => line.Contains("target=AppX"));
                    int index;
                    var search = "file=";
                    if (msixLine == null || (index = msixLine.IndexOf(search)) == -1)
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
                    _logger.LogError(ex, "Failed to package 'msix'.");
                    throw new MSStoreException("Failed to generate msix package.", ex);
                }
            });
        }

        public async Task<int> PublishAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? inputDirectory, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, electronProjectFile) = GetInfo(pathOrUrl);

            // Try to find AppId inside the package.json file
            app = await storePackagedAPI.EnsureAppInitializedAsync(
                app,
                () => GetAppIdFromPackageJsonAsync(electronProjectFile, ct),
                ct);

            if (app?.Id == null)
            {
                return -1;
            }

            AnsiConsole.MarkupLine($"AppId: [green bold]{app.Id}[/]");

            if (inputDirectory == null)
            {
                inputDirectory = new DirectoryInfo(Path.Combine(projectRootPath.FullName, "dist"));
            }

            var output = projectRootPath.CreateSubdirectory(Path.Join("dist"));

            var packageFiles = inputDirectory.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                                     .Where(f => f.Extension == ".appx");

            return await storePackagedAPI.PublishAsync(app, GetFirstSubmissionDataAsync, output, packageFiles, _browserLauncher, _consoleReader, _zipFileManager, _fileDownloader, _azureBlobManager, _logger, ct);
        }

        private Task<(string?, List<SubmissionImage>)> GetFirstSubmissionDataAsync(string listingLanguage, CancellationToken ct)
        {
            var description = "My Electron App";
            var images = new List<SubmissionImage>();
            return Task.FromResult<(string?, List<SubmissionImage>)>((description, images));
        }

        private async Task<string?> GetAppIdFromPackageJsonAsync(FileInfo electronProjectFile, CancellationToken ct)
        {
            var electronManifest = await _electronManifestManager.LoadAsync(electronProjectFile, ct);

            return electronManifest.MSStoreCLIAppID;
        }
    }
}
