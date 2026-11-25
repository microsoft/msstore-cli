// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.Helpers
{
    /// <summary>
    /// Reads which architecture, device family and minimum OS version each package targets,
    /// to decide which existing Store packages a new upload replaces.
    /// </summary>
    internal static class PackageTargets
    {
        private const string MinVersionSeparator = " min version ";
        private static readonly string[] PackageExtensions = [".msix", ".appx", ".msixbundle", ".appxbundle"];

        public static List<Target>? FromStorePackage(ApplicationPackage package)
        {
            if (string.IsNullOrWhiteSpace(package.Architecture) || package.TargetDeviceFamilies is not { Count: > 0 })
            {
                return null;
            }

            var targets = new List<Target>();
            foreach (var deviceFamily in package.TargetDeviceFamilies)
            {
                var parts = deviceFamily.Split(MinVersionSeparator, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !Version.TryParse(parts[1], out var minVersion))
                {
                    return null;
                }

                targets.Add(new Target(package.Architecture.Trim(), parts[0], minVersion));
            }

            return targets;
        }

        public static List<Target>? FromFile(FileInfo file)
        {
            try
            {
                using var archive = ZipFile.OpenRead(file.FullName);
                return ReadArchive(archive);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static bool Supersedes(List<Target> newTargets, List<Target> existingTargets)
            => existingTargets.All(existing => newTargets.Any(target =>
                string.Equals(target.Architecture, existing.Architecture, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(target.DeviceFamily, existing.DeviceFamily, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(target.DeviceFamily, "Windows.Universal", StringComparison.OrdinalIgnoreCase))
                && target.MinVersion <= existing.MinVersion));

        private static List<Target>? ReadArchive(ZipArchive archive)
        {
            var appxManifest = archive.GetEntry("AppxManifest.xml");
            if (appxManifest != null)
            {
                return ReadAppxManifest(appxManifest);
            }

            // Bundles list their packages in a manifest; .msixupload/.appxupload files hold them at the root.
            var bundleManifest = archive.GetEntry("AppxMetadata/AppxBundleManifest.xml");
            var innerPackages = bundleManifest != null
                ? GetBundlePackages(archive, bundleManifest)
                : archive.Entries.Where(e => !e.FullName.Contains('/', StringComparison.Ordinal)
                    && PackageExtensions.Contains(Path.GetExtension(e.FullName), StringComparer.OrdinalIgnoreCase));

            var targets = new List<Target>();
            foreach (var entry in innerPackages)
            {
                var innerTargets = entry != null ? ReadNestedArchive(entry) : null;
                if (innerTargets == null)
                {
                    return null;
                }

                targets.AddRange(innerTargets);
            }

            return targets.Count > 0 ? targets : null;
        }

        private static IEnumerable<ZipArchiveEntry?> GetBundlePackages(ZipArchive archive, ZipArchiveEntry bundleManifest)
        {
            var packages = LoadXml(bundleManifest).SelectNodes("/*[local-name()='Bundle']/*[local-name()='Packages']/*[local-name()='Package'][@Type='application']");
            foreach (var package in packages?.OfType<XmlElement>() ?? [])
            {
                var fileName = package.GetAttribute("FileName");
                yield return archive.GetEntry(fileName) ?? archive.GetEntry(Uri.EscapeDataString(fileName));
            }
        }

        private static List<Target>? ReadNestedArchive(ZipArchiveEntry entry)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"msstore_{Path.GetRandomFileName()}");
            try
            {
                entry.ExtractToFile(tempFile);
                using var nested = ZipFile.OpenRead(tempFile);
                return ReadArchive(nested);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        private static List<Target>? ReadAppxManifest(ZipArchiveEntry manifest)
        {
            var root = LoadXml(manifest).DocumentElement;
            var identity = root?.SelectSingleNode("*[local-name()='Identity']") as XmlElement;
            var architecture = identity?.GetAttribute("ProcessorArchitecture");
            if (string.IsNullOrWhiteSpace(architecture))
            {
                architecture = "neutral";
            }

            var targets = new List<Target>();
            var deviceFamilies = root?.SelectNodes("*[local-name()='Dependencies']/*[local-name()='TargetDeviceFamily']");
            foreach (var deviceFamily in deviceFamilies?.OfType<XmlElement>() ?? [])
            {
                if (!Version.TryParse(deviceFamily.GetAttribute("MinVersion"), out var minVersion))
                {
                    return null;
                }

                targets.Add(new Target(architecture, deviceFamily.GetAttribute("Name"), minVersion));
            }

            return targets.Count > 0 ? targets : null;
        }

        private static XmlDocument LoadXml(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var xmlDoc = new XmlDocument { XmlResolver = null };
            xmlDoc.Load(reader);
            return xmlDoc;
        }

        internal sealed record Target(string Architecture, string DeviceFamily, Version MinVersion);
    }
}
