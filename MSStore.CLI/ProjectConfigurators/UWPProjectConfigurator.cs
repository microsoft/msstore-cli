// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
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

        public override string[] PackageFilesExtensionInclude => new[] { ".msixupload", ".appxupload" };
        public override string[]? PackageFilesExtensionExclude { get; }
        public override SearchOption PackageFilesSearchOption { get; } = SearchOption.TopDirectoryOnly;
        public override PublishFileSearchFilterStrategy PublishFileSearchFilterStrategy { get; } = PublishFileSearchFilterStrategy.Newest;
        public override string OutputSubdirectory { get; } = Path.Combine("obj", "MSStore.CLI");
        public override string DefaultInputSubdirectory { get; } = "AppPackages";
        public override IEnumerable<BuildArch>? DefaultBuildArchs => new[] { BuildArch.X64, BuildArch.Arm64 };

        public override bool PackageOnlyOnWindows => true;

        public override Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, Version? version, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, manifestFile) = GetInfo(pathOrUrl);

            UpdateManifest(manifestFile.FullName, app, publisherDisplayName, version);

            AnsiConsole.WriteLine($"UWP project at '{projectRootPath.FullName}' is now configured to build to the Microsoft Store!");
            AnsiConsole.MarkupLine("For more information on building your UWP project to the Microsoft Store, see [link]https://learn.microsoft.com/windows/msix/package/packaging-uwp-apps[/]");

            return Task.FromResult((0, output));
        }

        public override Task<List<string>?> GetAppImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            var (_, manifestFile) = GetInfo(pathOrUrl);

            var allImagesInManifest = GetAllImagesFromManifest(manifestFile, Logger);

            return Task.FromResult<List<string>?>(allImagesInManifest);
        }

        public override Task<List<string>?> GetDefaultImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            // Already covered by the default UWP images from the Windows SDK
            return Task.FromResult<List<string>?>(null);
        }

        internal static void UpdateManifest(string appxManifestPath, DevCenterApplication app, string publisherDisplayName, Version? version)
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

                if (version != null)
                {
                    var versionAttribute = identity.Attributes?["Version"];
                    if (versionAttribute != null)
                    {
                        versionAttribute.Value = version.ToVersionString();
                    }
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
                var visualElements = application.SelectSingleNode("//uap:VisualElements", nsmgr);
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

        internal static void UpdateManifestVersion(string appxManifestPath, Version version)
        {
            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(appxManifestPath);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

            var identity = xmlDoc.SelectSingleNode("/ns:Package/ns:Identity", nsmgr);
            if (identity != null)
            {
                var versionAttribute = identity.Attributes?["Version"];
                if (versionAttribute != null)
                {
                    versionAttribute.Value = version.ToVersionString();
                }
            }

            xmlDoc.Save(appxManifestPath);
        }

        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, Version? version, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            (DirectoryInfo projectRootPath, FileInfo manifestFile) = GetInfo(pathOrUrl);

            return await PackageAsync(projectRootPath, buildArchs, null, PackageFilesExtensionInclude, manifestFile, version, output, _externalCommandExecutor, Logger, ct);
        }

        internal static async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(DirectoryInfo projectRootPath, IEnumerable<BuildArch>? buildArchs, FileInfo? solutionPath, string[] packageFilesExtensionInclude, FileInfo appxManifestFile, Version? version, DirectoryInfo? output, IExternalCommandExecutor externalCommandExecutor, ILogger logger, CancellationToken ct)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            var vswhere = Path.Combine(programFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");

            if (!File.Exists(vswhere))
            {
                AnsiConsole.MarkupLine("[red]Visual Studio 2017 or later is required to package UWP apps[/]");
                return (-1, null);
            }

            var workingDirectory = solutionPath?.Directory?.FullName ?? projectRootPath.FullName;

            var msbuildPath = await AnsiConsole.Status().StartAsync("Finding MSBuild...", async ctx =>
            {
                try
                {
                    var result = await externalCommandExecutor.RunAsync($"\"{vswhere}\"", "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe", workingDirectory, ct);
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
                    logger.LogError(ex, "Could not find MSBuild.");
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
                    var result = await externalCommandExecutor.RunAsync($"\"{msbuildPath}\"", msBuildParams, workingDirectory, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    ctx.SuccessStatus("Packages restored successfully!");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to restore packages.");
                    throw new MSStoreException("Failed to restore packages.");
                }
            });

            output ??= new DirectoryInfo(Path.Combine(projectRootPath.FullName, "AppPackages"));

            if (version != null)
            {
                UpdateManifestVersion(appxManifestFile.FullName, version);
            }

            var bundleUploadFile = await AnsiConsole.Status().StartAsync("Building MSIX...", async ctx =>
            {
                try
                {
                    string platform;
                    string appxBundlePlatforms;
                    if (buildArchs?.Any() == true)
                    {
                        platform = buildArchs.First().ToString().ToUpperInvariant();
                        appxBundlePlatforms = string.Join("|", buildArchs.Select(a => a.ToString().ToUpperInvariant()));
                    }
                    else
                    {
                        platform = "X64";
                        appxBundlePlatforms = "X64|ARM64";
                    }

                    var msBuildParams = $"/p:Configuration=Release;AppxBundle=Always;Platform={platform};AppxBundlePlatforms=\"{appxBundlePlatforms}\";AppxPackageDir=\"{output.FullName}\";UapAppxPackageBuildMode=StoreUpload";

                    var result = await externalCommandExecutor.RunAsync($"(\"{msbuildPath}\"", $"{msBuildParams})", workingDirectory, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    var bundleUploadFile = result
                        .StdOut
                        .Split(Environment.NewLine)
                        .FirstOrDefault(l =>
                        {
                            foreach (string extension in packageFilesExtensionInclude)
                            {
                                if (l.Contains(extension))
                                {
                                    return true;
                                }
                            }

                            return false;
                        })
                        ?.Split("->")
                        ?.Last()
                        ?.Trim();

                    if (bundleUploadFile == null)
                    {
                        throw new MSStoreException($"Could not find any file with extensions {string.Join(", ", $"'{packageFilesExtensionInclude}'")}!");
                    }

                    ctx.SuccessStatus("MSIX built successfully!");

                    return bundleUploadFile;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to build MSIX.");
                    throw new MSStoreException("Failed to build MSIX.", ex);
                }
            });

            return (0, bundleUploadFile != null ? new FileInfo(bundleUploadFile).Directory : null);
        }

        public override Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct)
        {
            if (fileInfo == null)
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult(GetAppId(fileInfo));
        }

        internal static string? GetAppId(FileInfo fileInfo)
        {
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
            return buildItemAppId?.Attributes?["Value"]?.Value;
        }

        internal static List<string> GetAllImagesFromManifest(FileInfo appxManifest, ILogger logger)
        {
            XmlDocument xmlDoc = new XmlDocument();

            xmlDoc.Load(appxManifest.FullName);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            nsmgr.AddNamespace("ns2", "http://schemas.microsoft.com/appx/2013/manifest");
            nsmgr.AddNamespace("ns3", "http://schemas.microsoft.com/appx/2014/manifest");
            nsmgr.AddNamespace("mp", "http://schemas.microsoft.com/appx/2014/phone/manifest");
            nsmgr.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");
            var buildNamespace = "http://schemas.microsoft.com/developer/appx/2015/build";
            nsmgr.AddNamespace("build", buildNamespace);

            var images = new List<string>();
            var application = xmlDoc.SelectSingleNode("/ns:Package/ns:Applications", nsmgr)?.ChildNodes?[0];
            if (application != null)
            {
                // Store Logo
                var logoElement = application.SelectNodes("//ns:Properties/ns:Logo", nsmgr)?.OfType<XmlElement>();
                if (logoElement?.Any() == true)
                {
                    var value = logoElement.Single().InnerText;
                    if (value != null)
                    {
                        images.AddRange(GetAllImagesFiles(value, appxManifest, logger));
                    }
                }

                foreach (XmlElement applicationElement in application.ChildNodes)
                {
                    // App logo
                    var appLogoAttr = applicationElement.SelectNodes("//ns:VisualElements/@Logo", nsmgr)?.OfType<XmlAttribute>();
                    if (appLogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(appLogoAttr.Single().Value, appxManifest, logger));
                    }

                    // App small logo
                    var appSmallLogoAttr = applicationElement.SelectNodes("//ns:VisualElements/@SmallLogo", nsmgr)?.OfType<XmlAttribute>();
                    if (appSmallLogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(appSmallLogoAttr.Single().Value, appxManifest, logger));
                    }

                    // Default tile wide logo
                    var wideLogoAttr = applicationElement.SelectNodes("//ns:VisualElements/ns:DefaultTile/@WideLogo", nsmgr)?.OfType<XmlAttribute>();
                    if (wideLogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(wideLogoAttr.Single().Value, appxManifest, logger));
                    }

                    // Square30x30Logo
                    var square30x30LogoAttr = applicationElement.SelectNodes("//ns2:VisualElements/@Square30x30Logo", nsmgr)?.OfType<XmlAttribute>();
                    if (square30x30LogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(square30x30LogoAttr.Single().Value, appxManifest, logger));
                    }

                    // Square44x44Logo
                    var square44x44LogoAttr = applicationElement.SelectNodes("//*[local-name()='VisualElements']/@Square44x44Logo", nsmgr)?.OfType<XmlAttribute>();
                    if (square44x44LogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(square44x44LogoAttr.Single().Value, appxManifest, logger));
                    }

                    // Square70x70Logo
                    var square70x70LogoAttr = applicationElement.SelectNodes("//ns2:VisualElements/ns2:DefaultTile/@Square70x70Logo", nsmgr)?.OfType<XmlAttribute>();
                    if (square70x70LogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(square70x70LogoAttr.Single().Value, appxManifest, logger));
                    }

                    // Square71x71Logo
                    var square71x71LogoAttr = applicationElement.SelectNodes("//ns3:VisualElements/ns3:DefaultTile/@Square71x71Logo", nsmgr)?.OfType<XmlAttribute>();
                    if (square71x71LogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(square71x71LogoAttr.Single().Value, appxManifest, logger));
                    }

                    // Square150x150Logo
                    var square150x150LogoAttr = applicationElement.SelectNodes("//*[local-name()='VisualElements']/@Square150x150Logo", nsmgr)?.OfType<XmlAttribute>();
                    if (square150x150LogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(square150x150LogoAttr.Single().Value, appxManifest, logger));
                    }

                    // Wide310x150Logo
                    var wide310x150LogoAttr = applicationElement.SelectNodes("//*[local-name()='VisualElements']/*[local-name()='DefaultTile']/@Wide310x150Logo", nsmgr)?.OfType<XmlAttribute>();
                    if (wide310x150LogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(wide310x150LogoAttr.Single().Value, appxManifest, logger));
                    }

                    // Square310x310Logo
                    var square310x310LogoAttr = applicationElement.SelectNodes("//*[local-name()='VisualElements']/*[local-name()='DefaultTile']/@Square310x310Logo", nsmgr)?.OfType<XmlAttribute>();
                    if (square310x310LogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(square310x310LogoAttr.Single().Value, appxManifest, logger));
                    }

                    // Splash screen image
                    var splashScreenImageAttr = applicationElement.SelectNodes("//*[local-name()='VisualElements']/*[local-name()='SplashScreen']/@Image", nsmgr)?.OfType<XmlAttribute>();
                    if (splashScreenImageAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(splashScreenImageAttr.Single().Value, appxManifest, logger));
                    }

                    // lock screen badge
                    var lockScreenLogoAttr = applicationElement.SelectNodes("//*[local-name()='VisualElements']/*[local-name()='LockScreen']/@BadgeLogo", nsmgr)?.OfType<XmlAttribute>();
                    if (lockScreenLogoAttr?.Any() == true)
                    {
                        images.AddRange(GetAllImagesFiles(lockScreenLogoAttr.Single().Value, appxManifest, logger));
                    }
                }
            }

            return images.Distinct().ToList();
        }

        private static List<string> GetAllImagesFiles(string imageNodeText, FileInfo appxManifest, ILogger logger)
        {
            List<string> imagePaths = new List<string>();
            try
            {
                string installLocation = appxManifest.Directory?.FullName ?? string.Empty;
                List<string> directories = new List<string>(Directory.EnumerateDirectories(installLocation))
                {
                    installLocation + "\\"
                };

                foreach (string directory in directories)
                {
                    string imagePath = Path.Combine(directory, imageNodeText);
                    string imageLocation = Path.GetDirectoryName(imagePath) ?? string.Empty;
                    string imageFileWithoutExtension = Path.GetFileNameWithoutExtension(imagePath);
                    string imageFileExtension = Path.GetExtension(imagePath);

                    logger.LogInformation("Checking directory {Directory} for subfolder {ImageNodeText} forming full path {ImageLocation}", directory, imageNodeText, imageLocation);

                    if (Directory.Exists(imageLocation))
                    {
                        logger.LogInformation("Found path to image files, trying directory {ImageLocation} with search term {ImageFileWithoutExtension}", imageLocation, imageFileWithoutExtension);
                        imagePaths.AddRange(Directory.GetFiles(imageLocation, imageFileWithoutExtension + imageFileExtension, SearchOption.TopDirectoryOnly));
                        imagePaths.AddRange(Directory.GetFiles(imageLocation, imageFileWithoutExtension + ".*" + imageFileExtension, SearchOption.TopDirectoryOnly));
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine("Failed to get the image file " + imageNodeText);
                AnsiConsole.WriteLine(ex.ToString());
            }

            if (!imagePaths.Any())
            {
                string imageFile = Path.Combine(appxManifest.Directory?.FullName ?? string.Empty, imageNodeText);
                if (File.Exists(imageFile))
                {
                    imagePaths.Add(imageFile);
                }
            }

            return imagePaths;
        }
    }
}
