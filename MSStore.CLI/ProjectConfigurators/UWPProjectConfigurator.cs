// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class UWPProjectConfigurator : IProjectConfigurator, IProjectPackager, IProjectPublisher
    {
        private readonly IExternalCommandExecutor _externalCommandExecutor;

        public UWPProjectConfigurator(IExternalCommandExecutor externalCommandExecutor)
        {
            _externalCommandExecutor = externalCommandExecutor ?? throw new ArgumentNullException(nameof(externalCommandExecutor));
        }

        public string ConfiguratorProjectType { get; } = "UWP";

        public string[] SupportedProjectPattern { get; } = new[] { "Package.appxmanifest" };

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

        private (DirectoryInfo projectRootPath, FileInfo flutterProjectFiles) GetInfo(string pathOrUrl)
        {
            DirectoryInfo projectRootPath = new DirectoryInfo(pathOrUrl);
            var manifestFiles = projectRootPath.GetFiles(SupportedProjectPattern.First(), SearchOption.TopDirectoryOnly);

            if (manifestFiles.Length == 0)
            {
                throw new InvalidOperationException("No 'Package.appxmanifest' file found in the project root directory.");
            }

            var manifestFile = manifestFiles.First();

            return (projectRootPath, manifestFile);
        }

        public Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, manifestFile) = GetInfo(pathOrUrl);

            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(manifestFile.FullName);

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

            xmlDoc.Save(manifestFile.FullName);

            AnsiConsole.WriteLine($"UWP project at '{projectRootPath.FullName}' is now configured to build to the Microsoft Store!");
            AnsiConsole.WriteLine("For more information on building your UWP project to the Microsoft Store, see https://learn.microsoft.com/windows/msix/package/packaging-uwp-apps");

            return Task.FromResult((0, output));
        }

        public async Task<(int returnCode, FileInfo? outputFile)> PackageAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
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
                    var result = await _externalCommandExecutor.RunAsync($"\"{vswhere}\" -latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe", projectRootPath.FullName, ct);
                    if (result.ExitCode == 0 && result.StdOut.Contains("MSBuild.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.SuccessStatus("Found MSBuild.");
                        return result.StdOut.Replace(Environment.NewLine, string.Empty);
                    }

                    AnsiConsole.MarkupLine("[red]Could not find MSBuild.[/]");

                    return null;
                }
                catch (Exception)
                {
                    // _logger...
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
                    var result = await _externalCommandExecutor.RunAsync($"\"{msbuildPath}\" {msBuildParams}", projectRootPath.FullName, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    ctx.SuccessStatus("Packages restored successfully!");
                }
                catch (Exception)
                {
                    // _logger...
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
                    var result = await _externalCommandExecutor.RunAsync($"(\"{msbuildPath}\" {msBuildParams})", projectRootPath.FullName, ct);
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
                catch (Exception)
                {
                    // _logger...
                    throw new MSStoreException("Failed to build MSIX.");
                }
            });

            return (0, msixUploadFile != null ? new FileInfo(msixUploadFile) : null);
        }

        public async Task<int> PublishAsync(string pathOrUrl, DevCenterApplication? app, FileInfo? input, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, manifestFile) = GetInfo(pathOrUrl);

            if (app == null)
            {
                // Try to find AppId inside the manifest file
                string? appId = GetAppIdFromManifest(manifestFile);

                if (appId == null)
                {
                    throw new MSStoreException("Failed to find the AppId in the AppX Manifest file.");
                }

                var success = await AnsiConsole.Status().StartAsync("Retrieving application...", async ctx =>
                {
                    try
                    {
                        app = await storePackagedAPI.GetApplicationAsync(appId, ct);

                        ctx.SuccessStatus("Ok! Found the app!");
                    }
                    catch (Exception)
                    {
                        ctx.ErrorStatus("Could not retrieve your application. Please make sure you have the correct AppId.");

                        return false;
                    }

                    return true;
                });

                if (!success)
                {
                    return -1;
                }
            }

            if (app?.Id == null)
            {
                return -1;
            }

            AnsiConsole.MarkupLine($"AppId: [green bold]{app.Id}[/]");

            if (input == null)
            {
                var buildDirInfo = new DirectoryInfo(Path.Combine(projectRootPath.FullName, "AppPackages"));
                var msixUploads = buildDirInfo.GetFiles("*.msixupload");
                input = msixUploads.FirstOrDefault();
                if (input == null)
                {
                    return -1;
                }
            }

            var msixUploadPath = input.FullName;
            AnsiConsole.MarkupLine($"MSIX: [green bold]{msixUploadPath}[/]");

            AnsiConsole.MarkupLine("[yellow]TODO: Publish[/]");

            return -1;
        }

        private string? GetAppIdFromManifest(FileInfo manifestFile)
        {
            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(manifestFile.FullName);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            nsmgr.AddNamespace("mp", "http://schemas.microsoft.com/appx/2014/phone/manifest");
            nsmgr.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");
            var buildNamespace = "http://schemas.microsoft.com/developer/appx/2015/build";
            nsmgr.AddNamespace("build", buildNamespace);

            var buildItemAppId = xmlDoc.SelectSingleNode("/ns:Package/build:Metadata/build:Item[@Name='MSStoreCLIAppId']", nsmgr);

            return buildItemAppId?.Attributes?["Value"]?.Value;
        }
    }
}
