// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class MarkReplacedPackagesUnitTests
    {
        private DirectoryInfo _tempDir = null!;

        [TestInitialize]
        public void Initialize()
        {
            _tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MSStore", "ReplacedPackagesTests", Path.GetRandomFileName()));
        }

        [TestCleanup]
        public void Cleanup()
        {
            _tempDir.Delete(true);
        }

        [TestMethod]
        public void ReplacesEveryOlderPackageWithTheSameTargets()
        {
            var packages = Enumerable.Range(0, 30)
                .Select(i => StorePackage($"old_{i}.msix", "x64", "Windows.Desktop min version 10.0.17763.0"))
                .ToList();

            var count = Mark(packages, CreateMsix("new.msix", "x64", ("Windows.Desktop", "10.0.17763.0")));

            count.Should().Be(30);
            packages.Where(p => p.FileStatus == FileStatus.PendingDelete).Should().HaveCount(30);
        }

        [TestMethod]
        public void KeepsPackagesForOtherArchitecturesAndDeviceFamilies()
        {
            var x64 = StorePackage("x64.msix", "x64", "Windows.Desktop min version 10.0.17763.0");
            var arm64 = StorePackage("arm64.msix", "arm64", "Windows.Desktop min version 10.0.17763.0");
            var xbox = StorePackage("xbox.msix", "arm64", "Windows.Xbox min version 10.0.17763.0");
            var packages = new List<ApplicationPackage> { x64, arm64, xbox };

            Mark(packages, CreateMsix("new.msix", "arm64", ("Windows.Desktop", "10.0.17763.0")));

            arm64.FileStatus.Should().Be(FileStatus.PendingDelete);
            x64.FileStatus.Should().Be(FileStatus.Uploaded);
            xbox.FileStatus.Should().Be(FileStatus.Uploaded);
        }

        [TestMethod]
        public void KeepsPackagesThatSupportOlderWindowsVersions()
        {
            var old = StorePackage("old.msix", "x64", "Windows.Desktop min version 10.0.17763.0");
            var packages = new List<ApplicationPackage> { old };

            Mark(packages, CreateMsix("new.msix", "x64", ("Windows.Desktop", "10.0.22000.0")));

            old.FileStatus.Should().Be(FileStatus.Uploaded);
        }

        [TestMethod]
        public void ReadsArchitecturesFromBundlesInsideUploadFiles()
        {
            var x64 = StorePackage("x64.msix", "x64", "Windows.Desktop min version 10.0.17763.0");
            var arm64 = StorePackage("arm64.msix", "arm64", "Windows.Desktop min version 10.0.17763.0");
            var x86 = StorePackage("x86.msix", "x86", "Windows.Desktop min version 10.0.17763.0");
            var packages = new List<ApplicationPackage> { x64, arm64, x86 };

            var bundle = CreateBundle(
                "app.msixbundle",
                CreateMsix("app_x64.msix", "x64", ("Windows.Desktop", "10.0.17763.0")),
                CreateMsix("app_arm64.msix", "arm64", ("Windows.Desktop", "10.0.17763.0")));
            var upload = CreateZip("app.msixupload", bundle);

            Mark(packages, upload);

            x64.FileStatus.Should().Be(FileStatus.PendingDelete);
            arm64.FileStatus.Should().Be(FileStatus.PendingDelete);
            x86.FileStatus.Should().Be(FileStatus.Uploaded);
        }

        [TestMethod]
        public void ComparesMinimumVersionPerArchitectureInBundles()
        {
            var x64 = StorePackage("x64.msix", "x64", "Windows.Desktop min version 10.0.17763.0");
            var arm64 = StorePackage("arm64.msix", "arm64", "Windows.Desktop min version 10.0.17763.0");
            var packages = new List<ApplicationPackage> { x64, arm64 };

            var bundle = CreateBundle(
                "app.msixbundle",
                CreateMsix("app_x64.msix", "x64", ("Windows.Desktop", "10.0.22000.0")),
                CreateMsix("app_arm64.msix", "arm64", ("Windows.Desktop", "10.0.17763.0")));

            Mark(packages, bundle);

            x64.FileStatus.Should().Be(FileStatus.Uploaded);
            arm64.FileStatus.Should().Be(FileStatus.PendingDelete);
        }

        [TestMethod]
        public void FallsBackToReplacingOnePackageWhenStoreMinimumVersionIsMissing()
        {
            var packages = new List<ApplicationPackage>
            {
                StorePackage("a.msix", "x64", "Windows.Desktop"),
                StorePackage("b.msix", "x64", "Windows.Desktop"),
            };

            var count = Mark(packages, CreateMsix("new.msix", "x64", ("Windows.Desktop", "10.0.17763.0")));

            count.Should().Be(1);
            packages[0].FileStatus.Should().Be(FileStatus.PendingDelete);
            packages[1].FileStatus.Should().Be(FileStatus.Uploaded);
        }

        [TestMethod]
        public void FallsBackToReplacingOnePackageWithTheSameExtensionWhenTargetsAreUnknown()
        {
            var packages = new List<ApplicationPackage>
            {
                StorePackage("a.msix", "x64", "Windows.Desktop min version 10.0.17763.0"),
                StorePackage("b.msix", "x64", "Windows.Desktop min version 10.0.17763.0"),
            };
            var notAPackage = new FileInfo(Path.Combine(_tempDir.FullName, "new.msix"));
            File.WriteAllText(notAPackage.FullName, "not a zip");

            var count = Mark(packages, notAPackage);

            count.Should().Be(1);
            packages[0].FileStatus.Should().Be(FileStatus.PendingDelete);
            packages[1].FileStatus.Should().Be(FileStatus.Uploaded);
        }

        [TestMethod]
        public void RemovesPendingUploadsLeftFromAPreviousAttempt()
        {
            var leftovers = new[]
            {
                new ApplicationPackage { FileName = "first.msix", FileStatus = FileStatus.PendingUpload },
                new ApplicationPackage { FileName = "second.msix", FileStatus = FileStatus.PendingUpload },
            };
            var packages = leftovers.ToList();

            Mark(packages, CreateMsix("new.msix", "x64", ("Windows.Desktop", "10.0.17763.0")));

            packages.Should().BeEmpty();
        }

        private static int Mark(List<ApplicationPackage> packages, FileInfo file)
            => IStorePackagedAPIExtensions.MarkReplacedPackages(packages, [file], NullLogger.Instance);

        private static ApplicationPackage StorePackage(string fileName, string architecture, string deviceFamily)
            => new()
            {
                FileName = fileName,
                FileStatus = FileStatus.Uploaded,
                Architecture = architecture,
                TargetDeviceFamilies = [deviceFamily],
            };

        private FileInfo CreateMsix(string fileName, string architecture, params (string Name, string MinVersion)[] deviceFamilies)
        {
            var families = string.Concat(deviceFamilies.Select(f => $@"<TargetDeviceFamily Name=""{f.Name}"" MinVersion=""{f.MinVersion}"" MaxVersionTested=""10.0.22621.0"" />"));
            var manifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
  <Identity Name=""App"" Publisher=""CN=Test"" Version=""1.0.0.0"" ProcessorArchitecture=""{architecture}"" />
  <Dependencies>{families}</Dependencies>
</Package>";
            return CreateZip(fileName, ("AppxManifest.xml", manifest));
        }

        private FileInfo CreateBundle(string fileName, params FileInfo[] innerPackages)
        {
            var packageElements = string.Concat(innerPackages.Select(p => $@"<Package Type=""application"" FileName=""{p.Name}"" />"));
            var manifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Bundle xmlns=""http://schemas.microsoft.com/appx/2013/bundle"">
  <Packages>{packageElements}</Packages>
</Bundle>";

            var bundle = CreateZip(fileName, innerPackages);
            using (var archive = ZipFile.Open(bundle.FullName, ZipArchiveMode.Update))
            {
                using var writer = new StreamWriter(archive.CreateEntry("AppxMetadata/AppxBundleManifest.xml").Open());
                writer.Write(manifest);
            }

            return bundle;
        }

        private FileInfo CreateZip(string fileName, params FileInfo[] files)
        {
            var path = Path.Combine(_tempDir.FullName, fileName);
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    archive.CreateEntryFromFile(file.FullName, file.Name);
                }
            }

            return new FileInfo(path);
        }

        private FileInfo CreateZip(string fileName, (string EntryName, string Content) entry)
        {
            var path = Path.Combine(_tempDir.FullName, fileName);
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(archive.CreateEntry(entry.EntryName).Open());
                writer.Write(entry.Content);
            }

            return new FileInfo(path);
        }
    }
}
