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
        private const string UniversalDeviceFamily = "Windows.Universal";
        private static readonly string[] PackageExtensions = [".msix", ".appx", ".msixbundle", ".appxbundle"];

        public static List<Target>? FromStorePackage(ApplicationPackage package)
        {
            if (string.IsNullOrWhiteSpace(package.Architecture)
                || package.TargetDeviceFamilies is not { Count: > 0 }
                || !Version.TryParse(package.Version, out var version))
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

                targets.Add(new Target(package.Architecture.Trim(), parts[0], minVersion, version));
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
        {
            foreach (var existing in existingTargets)
            {
                var newForArchitecture = newTargets.Where(t => Matches(t.Architecture, existing.Architecture)).ToList();
                var existingForArchitecture = existingTargets.Where(t => Matches(t.Architecture, existing.Architecture)).ToList();
                if (newForArchitecture.Count == 0 || newForArchitecture.Any(t => t.Version < existing.Version))
                {
                    return false;
                }

                // Every device family the existing package reaches must be reached by the new one on the same or an older OS.
                var families = existingForArchitecture.Concat(newForArchitecture).Select(t => t.DeviceFamily).Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (var family in families)
                {
                    var existingMin = EffectiveMinVersion(existingForArchitecture, family);
                    var newMin = EffectiveMinVersion(newForArchitecture, family);
                    if (existingMin != null && (newMin == null || newMin > existingMin))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // An explicit device family overrides Windows.Universal for that family.
        private static Version? EffectiveMinVersion(List<Target> targets, string deviceFamily)
            => (targets.FirstOrDefault(t => Matches(t.DeviceFamily, deviceFamily))
                ?? targets.FirstOrDefault(t => Matches(t.DeviceFamily, UniversalDeviceFamily)))?.MinVersion;

        private static bool Matches(string first, string second)
            => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

        private static List<Target>? ReadArchive(ZipArchive archive)
        {
            var appxManifest = archive.GetEntry("AppxManifest.xml");
            if (appxManifest != null)
            {
                return ReadAppxManifest(appxManifest);
            }

            var bundleManifest = archive.GetEntry("AppxMetadata/AppxBundleManifest.xml");
            if (bundleManifest != null)
            {
                return ReadBundle(archive, bundleManifest);
            }

            // .msixupload/.appxupload files hold their packages and bundles at the root.
            return ReadPackages(archive.Entries.Where(e => !e.FullName.Contains('/', StringComparison.Ordinal)
                && PackageExtensions.Contains(Path.GetExtension(e.FullName), StringComparer.OrdinalIgnoreCase)));
        }

        private static List<Target>? ReadBundle(ZipArchive archive, ZipArchiveEntry bundleManifest)
        {
            var root = LoadXml(bundleManifest).DocumentElement;
            var identity = root?.SelectSingleNode("*[local-name()='Identity']") as XmlElement;
            if (!Version.TryParse(identity?.GetAttribute("Version"), out var bundleVersion))
            {
                return null;
            }

            var entries = (root?.SelectNodes("*[local-name()='Packages']/*[local-name()='Package'][@Type='application']")?.OfType<XmlElement>() ?? [])
                .Select(package => package.GetAttribute("FileName"))
                .Select(fileName => archive.GetEntry(fileName) ?? archive.GetEntry(Uri.EscapeDataString(fileName)));

            // The Store reports the bundle's own version, which can differ from its inner packages.
            return ReadPackages(entries)?.Select(t => t with { Version = bundleVersion }).ToList();
        }

        private static List<Target>? ReadPackages(IEnumerable<ZipArchiveEntry?> entries)
        {
            var targets = new List<Target>();
            foreach (var entry in entries)
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

            if (!Version.TryParse(identity?.GetAttribute("Version"), out var version))
            {
                return null;
            }

            var targets = new List<Target>();
            var deviceFamilies = root?.SelectNodes("*[local-name()='Dependencies']/*[local-name()='TargetDeviceFamily']");
            foreach (var deviceFamily in deviceFamilies?.OfType<XmlElement>() ?? [])
            {
                if (!Version.TryParse(deviceFamily.GetAttribute("MinVersion"), out var minVersion))
                {
                    return null;
                }

                targets.Add(new Target(architecture, deviceFamily.GetAttribute("Name"), minVersion, version));
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

        internal sealed record Target(string Architecture, string DeviceFamily, Version MinVersion, Version Version);
    }
}
