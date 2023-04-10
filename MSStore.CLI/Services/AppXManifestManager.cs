// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using Spectre.Console;

namespace MSStore.CLI.Services
{
    internal class AppXManifestManager : IAppXManifestManager
    {
        private const string AppxBuildNamespace = "http://schemas.microsoft.com/developer/appx/2015/build";

        internal static XmlNamespaceManager GetXmlNamespaceManager(XmlNameTable nameTable)
        {
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(nameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            nsmgr.AddNamespace("ns2", "http://schemas.microsoft.com/appx/2013/manifest");
            nsmgr.AddNamespace("ns3", "http://schemas.microsoft.com/appx/2014/manifest");
            nsmgr.AddNamespace("mp", "http://schemas.microsoft.com/appx/2014/phone/manifest");
            nsmgr.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");
            nsmgr.AddNamespace("build", AppxBuildNamespace);
            return nsmgr;
        }

        public void UpdateManifest(string appxManifestPath, DevCenterApplication app, string publisherDisplayName, Version? version)
        {
            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(appxManifestPath);

            var nsmgr = GetXmlNamespaceManager(xmlDoc.NameTable);

            var (metadataWasAppened, buildItemAppIdWasAppended) = SetBuildAppId(app, xmlDoc, nsmgr);

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

            UpdatePhoneIdentity(xmlDoc, nsmgr);

            SetAppPackagePropertyDisplayName(app, xmlDoc, nsmgr);

            SetAppPackagePropertyPublisherDisplayName(publisherDisplayName, xmlDoc, nsmgr);

            SetAppDisplayName(app, xmlDoc, nsmgr);

            xmlDoc.Save(appxManifestPath);
            FixBuildAppIdSpacing(appxManifestPath, metadataWasAppened, buildItemAppIdWasAppended);
        }

        private static void FixBuildAppIdSpacing(string appxManifestPath, bool metadataWasAppened, bool buildItemAppIdWasAppended)
        {
            if (metadataWasAppened || buildItemAppIdWasAppended)
            {
                var appxManifestContent = File.ReadAllText(appxManifestPath);

                if (metadataWasAppened)
                {
                    var openBuildMetadataText = "<build:Metadata>";
                    var openBuildMetadataIndex = appxManifestContent.LastIndexOf(openBuildMetadataText, StringComparison.OrdinalIgnoreCase);
                    if (openBuildMetadataIndex >= 0)
                    {
                        appxManifestContent = appxManifestContent.Insert(openBuildMetadataIndex, "  ");
                    }
                }

                if (buildItemAppIdWasAppended)
                {
                    var closeBuildMetadataText = "</build:Metadata>";
                    var closeBuildMetadataIndex = appxManifestContent.LastIndexOf(closeBuildMetadataText, StringComparison.OrdinalIgnoreCase);
                    if (closeBuildMetadataIndex >= 0)
                    {
                        appxManifestContent = appxManifestContent.Insert(closeBuildMetadataIndex + closeBuildMetadataText.Length, Environment.NewLine);
                        appxManifestContent = appxManifestContent.Insert(closeBuildMetadataIndex, Environment.NewLine + "  ");
                    }

                    var buildItemNameText = "<build:Item Name=\"MSStoreCLIAppId\"";
                    var buildItemNameIndex = appxManifestContent.LastIndexOf(buildItemNameText, StringComparison.OrdinalIgnoreCase);
                    if (buildItemNameIndex >= 0)
                    {
                        appxManifestContent = appxManifestContent.Insert(buildItemNameIndex, Environment.NewLine + "    ");
                    }
                }

                File.WriteAllText(appxManifestPath, appxManifestContent);
            }
        }

        public void MinimalUpdateManifest(string appxManifestPath, DevCenterApplication app, string publisherDisplayName)
        {
            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(appxManifestPath);

            var nsmgr = GetXmlNamespaceManager(xmlDoc.NameTable);

            var (metadataWasAppened, buildItemAppIdWasAppended) = SetBuildAppId(app, xmlDoc, nsmgr);

            var identity = xmlDoc.SelectSingleNode("/ns:Package/ns:Identity", nsmgr);
            if (identity != null)
            {
                // Remove this set when Maui support this properly, since csproj property needs to be a GUID, but the manifest needs to be a string
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

            SetAppPackagePropertyPublisherDisplayName(publisherDisplayName, xmlDoc, nsmgr);

            xmlDoc.Save(appxManifestPath);
            FixBuildAppIdSpacing(appxManifestPath, metadataWasAppened, buildItemAppIdWasAppended);
        }

        private static void SetAppDisplayName(DevCenterApplication app, XmlDocument xmlDoc, XmlNamespaceManager nsmgr)
        {
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
        }

        private static void UpdatePhoneIdentity(XmlDocument xmlDoc, XmlNamespaceManager nsmgr)
        {
            var phoneIdentity = xmlDoc.SelectSingleNode("/ns:Package/mp:PhoneIdentity", nsmgr);
            if (phoneIdentity != null)
            {
                var phoneProductId = phoneIdentity.Attributes?["PhoneProductId"];
                if (phoneProductId != null)
                {
                    phoneProductId.Value = Guid.NewGuid().ToString();
                }
            }
        }

        private static (bool metadataWasAppened, bool buildItemAppIdWasAppended) SetBuildAppId(DevCenterApplication app, XmlDocument xmlDoc, XmlNamespaceManager nsmgr)
        {
            bool metadataWasAppened = false;
            bool buildItemAppIdWasAppended = false;

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
                    packageElement.SetAttribute("xmlns:build", AppxBuildNamespace);
                }

                var metadata = xmlDoc.SelectSingleNode("/ns:Package/build:Metadata", nsmgr);
                if (metadata == null)
                {
                    metadata = xmlDoc.CreateElement("build", "Metadata", AppxBuildNamespace);
                    package.AppendChild(metadata);
                    metadataWasAppened = true;
                }

                var buildItemAppId = metadata.SelectSingleNode("//build:Item[@Name='MSStoreCLIAppId']", nsmgr);
                if (buildItemAppId == null)
                {
                    buildItemAppId = xmlDoc.CreateElement("build", "Item", AppxBuildNamespace);
                    (buildItemAppId as XmlElement)?.SetAttribute("Name", "MSStoreCLIAppId");
                    metadata.AppendChild(buildItemAppId);
                    buildItemAppIdWasAppended = true;
                }

                (buildItemAppId as XmlElement)?.SetAttribute("Value", app.Id);
            }

            return (metadataWasAppened, buildItemAppIdWasAppended);
        }

        private static void SetAppPackagePropertyDisplayName(DevCenterApplication app, XmlDocument xmlDoc, XmlNamespaceManager nsmgr)
        {
            var properties = xmlDoc.SelectSingleNode("/ns:Package/ns:Properties", nsmgr);
            if (properties != null)
            {
                var displayName = properties?["DisplayName"];
                if (displayName != null)
                {
                    displayName.InnerText = app.PrimaryName ?? string.Empty;
                }
            }
        }

        private static void SetAppPackagePropertyPublisherDisplayName(string? publisherDisplayName, XmlDocument xmlDoc, XmlNamespaceManager nsmgr)
        {
            var properties = xmlDoc.SelectSingleNode("/ns:Package/ns:Properties", nsmgr);
            if (properties != null)
            {
                var publisherDisplayNameElement = properties?["PublisherDisplayName"];
                if (publisherDisplayNameElement != null)
                {
                    publisherDisplayNameElement.InnerText = publisherDisplayName ?? string.Empty;
                }
            }
        }

        public Version UpdateManifestVersion(string appxManifestPath, Version? version)
        {
            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(appxManifestPath);

            var nsmgr = GetXmlNamespaceManager(xmlDoc.NameTable);

            var identity = xmlDoc.SelectSingleNode("/ns:Package/ns:Identity", nsmgr);
            if (identity != null)
            {
                var versionAttribute = identity.Attributes?["Version"];
                if (versionAttribute != null)
                {
                    if (version != null)
                    {
                        versionAttribute.Value = version.ToVersionString();
                    }
                    else
                    {
                        if (!Version.TryParse(versionAttribute.Value, out version))
                        {
                            version = new Version(1, 0, 0, 0);
                        }
                    }
                }

                xmlDoc.Save(appxManifestPath);

                if (version != null)
                {
                    return version;
                }
            }

            return new Version(1, 0, 0, 0);
        }

        public string? GetAppId(FileInfo fileInfo)
        {
            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(fileInfo.FullName);

            var nsmgr = GetXmlNamespaceManager(xmlDoc.NameTable);

            var buildItemAppId = xmlDoc.SelectSingleNode("/ns:Package/build:Metadata/build:Item[@Name='MSStoreCLIAppId']", nsmgr);
            return buildItemAppId?.Attributes?["Value"]?.Value;
        }

        public List<string> GetAllImagesFromManifest(FileInfo appxManifest, ILogger logger)
        {
            XmlDocument xmlDoc = new XmlDocument();

            xmlDoc.Load(appxManifest.FullName);
            var nsmgr = GetXmlNamespaceManager(xmlDoc.NameTable);

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
