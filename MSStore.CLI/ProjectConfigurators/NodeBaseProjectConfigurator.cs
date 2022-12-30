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
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal abstract class NodeBaseProjectConfigurator : FileProjectConfigurator
    {
        public override string[] SupportedProjectPattern { get; } = new[] { "package.json" };

        protected IExternalCommandExecutor ExternalCommandExecutor { get; }

        protected NodeBaseProjectConfigurator(IExternalCommandExecutor externalCommandExecutor, IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, ILogger logger)
            : base(browserLauncher, consoleReader, zipFileManager, fileDownloader, azureBlobManager, logger)
        {
            ExternalCommandExecutor = externalCommandExecutor ?? throw new ArgumentNullException(nameof(externalCommandExecutor));
        }

        protected static bool IsYarn(DirectoryInfo projectRootPath)
        {
            return projectRootPath.GetFiles("yarn.lock", SearchOption.TopDirectoryOnly).Any();
        }

        private static Dictionary<string, bool> _npmInstallExecuted = new Dictionary<string, bool>();
        protected async Task<bool> RunNpmInstallAsync(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            if (_npmInstallExecuted.TryGetValue(projectRootPath.FullName, out var value) && value)
            {
                Logger.LogInformation("Using cache. Npm install already executed for {ProjectRootPath}.", projectRootPath.FullName);
                return true;
            }

            return await AnsiConsole.Status().StartAsync("Running 'npm install'...", async ctx =>
            {
                try
                {
                    var result = await ExternalCommandExecutor.RunAsync("npm", "install", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0)
                    {
                        ctx.SuccessStatus("'npm install' ran successfully.");
                        _npmInstallExecuted[projectRootPath.FullName] = true;
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

        private static Dictionary<string, bool> _yarnInstallExecuted = new Dictionary<string, bool>();
        protected async Task<bool> RunYarnInstallAsync(DirectoryInfo projectRootPath, CancellationToken ct)
        {
            if (_yarnInstallExecuted.TryGetValue(projectRootPath.FullName, out var value) && value)
            {
                Logger.LogInformation("Using cache. Yarn install already executed for {ProjectRootPath}.", projectRootPath.FullName);
                return true;
            }

            return await AnsiConsole.Status().StartAsync("Running 'yarn install'...", async ctx =>
            {
                try
                {
                    var result = await ExternalCommandExecutor.RunAsync("yarn", "install", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0)
                    {
                        ctx.SuccessStatus("'yarn install' ran successfully.");
                        _yarnInstallExecuted[projectRootPath.FullName] = true;
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

        private static Dictionary<(string rootPath, string packageName), bool> _npmListExecuted = new Dictionary<(string rootPath, string packageName), bool>();
        protected async Task<bool> NpmPackageExistsAsync(DirectoryInfo projectRootPath, string packageName, bool useCache = true, CancellationToken ct = default)
        {
            if (useCache && _npmListExecuted.TryGetValue((projectRootPath.FullName, packageName), out var value))
            {
                Logger.LogInformation("Using cache. Npm list already executed for {ProjectRootPath} and package {PackageName}. Result: {Result}", projectRootPath.FullName, packageName, value);
                return value;
            }

            return await AnsiConsole.Status().StartAsync($"Checking if package '{packageName}' is already installed...", async ctx =>
            {
                try
                {
                    var result = await ExternalCommandExecutor.RunAsync("npm", $"list {packageName}", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0 && result.StdOut.Contains($"`-- {packageName}@"))
                    {
                        ctx.SuccessStatus($"'{packageName}' package is already installed, no need to install it again.");
                        _npmListExecuted[(projectRootPath.FullName, packageName)] = true;
                        return true;
                    }

                    ctx.SuccessStatus($"'{packageName}' package is not yet installed.");
                    _npmListExecuted[(projectRootPath.FullName, packageName)] = false;
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'npm list {PackageName}'.", packageName);
                    throw new MSStoreException($"Failed to check if '{packageName}' package is already installed..");
                }
            });
        }

        private static Dictionary<(string rootPath, string packageName), bool> _yarnListExecuted = new Dictionary<(string rootPath, string packageName), bool>();
        protected async Task<bool> YarnPackageExistsAsync(DirectoryInfo projectRootPath, string packageName, bool useCache = true, CancellationToken ct = default)
        {
            if (useCache && _yarnListExecuted.TryGetValue((projectRootPath.FullName, packageName), out var value))
            {
                Logger.LogInformation("Using cache. Yarn list already executed for {ProjectRootPath} and package {PackageName}. Result: {Result}", projectRootPath.FullName, packageName, value);
                return value;
            }

            return await AnsiConsole.Status().StartAsync($"Checking if package '{packageName}' is already installed...", async ctx =>
            {
                try
                {
                    var result = await ExternalCommandExecutor.RunAsync("yarn", $"list --pattern {packageName}", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0 && result.StdOut.Contains($"─ {packageName}@"))
                    {
                        ctx.SuccessStatus($"'{packageName}' package is already installed, no need to install it again.");
                        _yarnListExecuted[(projectRootPath.FullName, packageName)] = true;
                        return true;
                    }

                    ctx.SuccessStatus($"'{packageName}' package is not yet installed.");
                    _yarnListExecuted[(projectRootPath.FullName, packageName)] = false;
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'yarn list {PackageName}'.", packageName);
                    throw new MSStoreException($"Failed to check if '{packageName}' package is already installed..");
                }
            });
        }

        protected Task<bool> InstallDependencyAsync(DirectoryInfo projectRootPath, string packageName, CancellationToken ct)
        {
            if (IsYarn(projectRootPath))
            {
                return InstallYarnDependencyAsync(projectRootPath, packageName, ct);
            }
            else
            {
                return InstallNpmDependencyAsync(projectRootPath, packageName, ct);
            }
        }

        private async Task<bool> InstallNpmDependencyAsync(DirectoryInfo projectRootPath, string packageName, CancellationToken ct)
        {
            var npmInstall = await RunNpmInstallAsync(projectRootPath, ct);

            if (!npmInstall)
            {
                throw new MSStoreException("Failed to run 'npm install'.");
            }

            var packageInstalled = await NpmPackageExistsAsync(projectRootPath, packageName, true, ct);

            if (packageInstalled)
            {
                return true;
            }

            AnsiConsole.WriteLine();

            return await AnsiConsole.Status().StartAsync($"Installing '{packageName}' package...", async ctx =>
            {
                try
                {
                    var result = await ExternalCommandExecutor.RunAsync("npm", $"install --save-dev {packageName}", projectRootPath.FullName, ct);
                    if (result.ExitCode != 0)
                    {
                        ctx.ErrorStatus($"'npm install --save-dev {packageName}' failed.");
                        return false;
                    }

                    ctx.SuccessStatus($"'{packageName}' package installed successfully!");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'npm install --save-dev {PackageName}'.", packageName);
                    throw new MSStoreException($"Failed to install '{packageName}' package.");
                }
            });
        }

        private async Task<bool> InstallYarnDependencyAsync(DirectoryInfo projectRootPath, string packageName, CancellationToken ct)
        {
            var yarnInstall = await RunYarnInstallAsync(projectRootPath, ct);

            if (!yarnInstall)
            {
                throw new MSStoreException("Failed to run 'yarn install'.");
            }

            var packageInstalled = await YarnPackageExistsAsync(projectRootPath, packageName, true, ct);

            if (packageInstalled)
            {
                return true;
            }

            AnsiConsole.WriteLine();

            return await AnsiConsole.Status().StartAsync($"Installing '{packageName}' package...", async ctx =>
            {
                try
                {
                    var result = await ExternalCommandExecutor.RunAsync("yarn", $"add --dev {packageName}", projectRootPath.FullName, ct);
                    if (result.ExitCode != 0)
                    {
                        ctx.ErrorStatus($"'yarn add --dev {packageName}' failed.");
                        return false;
                    }

                    ctx.SuccessStatus($"'{packageName}' package installed successfully!");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error running 'yarn add --dev {PackageName}'.", packageName);
                    throw new MSStoreException($"Failed to install '{packageName}' package.");
                }
            });
        }
    }
}
