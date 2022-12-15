// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class UWPProjectConfigurator : FileProjectConfigurator
    {
        private readonly IExternalCommandExecutor _externalCommandExecutor;

        public UWPProjectConfigurator(IExternalCommandExecutor externalCommandExecutor, IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, ILogger<UWPProjectConfigurator> logger)
            : base(browserLauncher, consoleReader, zipFileManager, fileDownloader, azureBlobManager, logger)
        {
            _externalCommandExecutor = externalCommandExecutor ?? throw new ArgumentNullException(nameof(externalCommandExecutor));
        }

        public override string ConfiguratorProjectType { get; } = "UWP";

        public override string[] SupportedProjectPattern { get; } = new[] { "Package.appxmanifest" };

        public override string[] PackageFilesExtensionInclude => new[] { ".msixupload" };
        public override string[]? PackageFilesExtensionExclude { get; }
        public override SearchOption PackageFilesSearchOption { get; } = SearchOption.TopDirectoryOnly;
        public override string OutputSubdirectory { get; } = Path.Join("obj", "MSStore.CLI");
        public override string DefaultInputSubdirectory { get; } = "AppPackages";

        public override Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, manifestFile) = GetInfo(pathOrUrl);

            UpdateManifest(manifestFile.FullName, app, publisherDisplayName);

            AnsiConsole.WriteLine($"UWP project at '{projectRootPath.FullName}' is now configured to build to the Microsoft Store!");
            AnsiConsole.MarkupLine("For more information on building your UWP project to the Microsoft Store, see [link]https://learn.microsoft.com/windows/msix/package/packaging-uwp-apps[/]");

            return Task.FromResult((0, output));
        }

        internal static void UpdateManifest(string appxManifestPath, DevCenterApplication app, string publisherDisplayName)
        {
            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(appxManifestPath);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            nsmgr.AddNamespace("mp", "http://schemas.microsoft.com/appx/2014/phone/manifest");
            nsmgr.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");
            var buildNamespace = "http://schemas.microsoft.com/developer/appx/2015/build";
            nsmgr.AddNamespace("build", buildNamespace);

            var package = xmlDoc.SelectSingleNode("/ns:Package", nsmgr);
            if (package != null)
            {
                var ignorableNamespaces = package.Attributes?["IgnorableNamespaces"];
                if (ignorableNamespaces != null)
                {
                    if (!string.IsNullOrEmpty(ignorableNamespaces.Value))
                    {
                        var ignorableNamespacesList = ignorableNamespaces.Value.Split(" ").ToList();
                        if (!ignorableNamespacesList.Contains("build"))
                        {
                            ignorableNamespacesList.Add("build");
                            ignorableNamespaces.Value = string.Join(" ", ignorableNamespacesList);
                        }
                    }
                    else
                    {
                        ignorableNamespaces.Value = "build";
                    }
                }

                var xmlnsBuild = package.Attributes?["xmlns:build"];
                if (xmlnsBuild == null && package is XmlElement packageElement)
                {
                    packageElement.SetAttribute("xmlns:build", buildNamespace);
                }

                var metadata = xmlDoc.SelectSingleNode("/ns:Package/build:Metadata", nsmgr);
                if (metadata == null)
                {
                    metadata = xmlDoc.CreateElement("build", "Metadata", buildNamespace);
                    package.AppendChild(metadata);
                }

                var buildItemAppId = metadata.SelectSingleNode("//build:Item[@Name='MSStoreCLIAppId']", nsmgr);
                if (buildItemAppId == null)
                {
                    buildItemAppId = xmlDoc.CreateElement("build", "Item", buildNamespace);
                    (buildItemAppId as XmlElement)?.SetAttribute("Name", "MSStoreCLIAppId");
                    metadata.AppendChild(buildItemAppId);
                }

                (buildItemAppId as XmlElement)?.SetAttribute("Value", app.Id);
            }

            var identity = xmlDoc.SelectSingleNode("/ns:Package/ns:Identity", nsmgr);
            if (identity != null)
            {
                var name = identity.Attributes?["Name"];
                if (name != null)
                {
                    name.Value = app.PackageIdentityName ?? string.Empty;
                }

                var publisher = identity.Attributes?["Publisher"];
                if (publisher != null)
                {
                    publisher.Value = app.PublisherName;
                }
            }

            var phoneIdentity = xmlDoc.SelectSingleNode("/ns:Package/mp:PhoneIdentity", nsmgr);
            if (phoneIdentity != null)
            {
                var phoneProductId = phoneIdentity.Attributes?["PhoneProductId"];
                if (phoneProductId != null)
                {
                    phoneProductId.Value = Guid.NewGuid().ToString();
                }
            }

            var properties = xmlDoc.SelectSingleNode("/ns:Package/ns:Properties", nsmgr);
            if (properties != null)
            {
                var displayName = properties?["DisplayName"];
                if (displayName != null)
                {
                    displayName.InnerText = app.PrimaryName ?? string.Empty;
                }

                var publisherDisplayNameElement = properties?["PublisherDisplayName"];
                if (publisherDisplayNameElement != null)
                {
                    publisherDisplayNameElement.InnerText = publisherDisplayName ?? string.Empty;
                }
            }

            var application = xmlDoc.SelectSingleNode("/ns:Package/ns:Applications", nsmgr)?.ChildNodes?[0];
            if (application != null)
            {
                var visualElements = application.SelectSingleNode("uap:VisualElements", nsmgr);
                if (visualElements != null)
                {
                    var displayName = visualElements.Attributes?["DisplayName"];
                    if (displayName != null)
                    {
                        displayName.Value = app.PrimaryName ?? string.Empty;
                    }
                }
            }

            xmlDoc.Save(appxManifestPath);
        }

        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, manifestFile) = GetInfo(pathOrUrl);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AnsiConsole.MarkupLine("[red]Packaging UWP apps is only supported on Windows[/]");
                return (-1, null);
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            var vswhere = Path.Combine(programFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");

            if (!File.Exists(vswhere))
            {
                AnsiConsole.MarkupLine("[red]Visual Studio 2017 or later is required to package UWP apps[/]");
                return (-1, null);
            }

            var msbuildPath = await AnsiConsole.Status().StartAsync("Finding MSBuild...", async ctx =>
            {
                try
                {
                    var result = await _externalCommandExecutor.RunAsync($"\"{vswhere}\"", "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0 && result.StdOut.Contains("MSBuild.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.SuccessStatus("Found MSBuild.");
                        return result.StdOut.Replace(Environment.NewLine, string.Empty);
                    }

                    AnsiConsole.MarkupLine("[red]Could not find MSBuild.[/]");

                    return null;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not find MSBuild.");
                    throw new MSStoreException("Could not find MSBuild.");
                }
            });

            if (string.IsNullOrEmpty(msbuildPath))
            {
                return (-1, null);
            }

            await AnsiConsole.Status().StartAsync("Restoring packages...", async ctx =>
            {
                try
                {
                    var msBuildParams = $"/t:restore";
                    var result = await _externalCommandExecutor.RunAsync($"\"{msbuildPath}\"", msBuildParams, projectRootPath.FullName, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    ctx.SuccessStatus("Packages restored successfully!");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to restore packages.");
                    throw new MSStoreException("Failed to restore packages.");
                }
            });

            if (output == null)
            {
                output = new DirectoryInfo(Path.Join(projectRootPath.FullName, "AppPackages"));
            }

            var msixUploadFile = await AnsiConsole.Status().StartAsync("Building MSIX...", async ctx =>
            {
                try
                {
                    var msBuildParams = $"/p:Configuration=Release;AppxBundle=Always;Platform=x64;AppxBundlePlatforms=\"x64|ARM64\";AppxPackageDir=\"{output.FullName}\";UapAppxPackageBuildMode=StoreUpload";
                    var result = await _externalCommandExecutor.RunAsync($"(\"{msbuildPath}\"", $"{msBuildParams})", projectRootPath.FullName, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    var msixUploadFile = result
                        .StdOut
                        .Split(Environment.NewLine)
                        .FirstOrDefault(l => l.Contains(".msixupload"))
                        ?.Split("->")
                        ?.Last()
                        ?.Trim();

                    if (msixUploadFile == null)
                    {
                        throw new MSStoreException("Could not find '.msixupload' file!");
                    }

                    ctx.SuccessStatus("MSIX built successfully!");

                    return msixUploadFile;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to build MSIX.");
                    throw new MSStoreException("Failed to build MSIX.");
                }
            });

            return (0, msixUploadFile != null ? new FileInfo(msixUploadFile).Directory : null);
        }

        public override Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct)
        {
            if (fileInfo == null)
            {
                return Task.FromResult<string?>(null);
            }

            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(fileInfo.FullName);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            nsmgr.AddNamespace("mp", "http://schemas.microsoft.com/appx/2014/phone/manifest");
            nsmgr.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");
            var buildNamespace = "http://schemas.microsoft.com/developer/appx/2015/build";
            nsmgr.AddNamespace("build", buildNamespace);

            var buildItemAppId = xmlDoc.SelectSingleNode("/ns:Package/build:Metadata/build:Item[@Name='MSStoreCLIAppId']", nsmgr);

            return Task.FromResult(buildItemAppId?.Attributes?["Value"]?.Value);
        }
    }
}
