// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.Commands.Init.Setup
{
    internal class UWPProjectConfigurator : IProjectConfigurator
    {
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

        public Task<int> ConfigureAsync(string pathOrUrl, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            DirectoryInfo projectRootPath = new DirectoryInfo(pathOrUrl);
            var manifestFiles = projectRootPath.GetFiles(SupportedProjectPattern.First(), SearchOption.TopDirectoryOnly);

            if (!manifestFiles.Any())
            {
                throw new InvalidOperationException("No 'Package.appxmanifest' file found in the project root directory.");
            }

            var manifestFile = manifestFiles.First();

            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(manifestFile.FullName);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            nsmgr.AddNamespace("mp", "http://schemas.microsoft.com/appx/2014/phone/manifest");
            nsmgr.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");

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

            return Task.FromResult(0);
        }
    }
}
