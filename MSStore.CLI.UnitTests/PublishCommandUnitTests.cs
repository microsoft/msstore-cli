// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.ProjectConfigurators;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class PublishCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddDefaultFakeAccount();
            AddFakeApps();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task PublishCommandShouldFailIfNoArgument()
        {
            await ParseAndInvokeAsync(
                new string[]
                {
                    "publish"
                });
        }

        [TestMethod]
        public async Task PublishCommandForUWPAppsShouldCallMSBuildIfWindows()
        {
            var path = CopyFilesRecursively("UWPProject");

            DefaultMSBuildExecution(new DirectoryInfo(path));

            UWPProjectConfigurator.UpdateManifest(Path.Combine(path, "Package.appxmanifest"), FakeApps[0], "publisher", null);
            var appPackagesFolder = Directory.CreateDirectory(Path.Combine(path, "AppPackages"));
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolder.FullName, "test.msixupload"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "publish",
                    path,
                    "--verbose"
                });

            result.Should().Contain("Submission commit success! Here is some data:");
            result.Should().Contain("test.msixupload");
        }

        [TestMethod]
        public async Task PublishCommandForWinUIAppsShouldCallMSBuildIfWindows()
        {
            var path = CopyFilesRecursively("WinUIProject");

            var dirInfo = new DirectoryInfo(path);
            DefaultMSBuildExecution(dirInfo);
            SetupWinUI(dirInfo);

            UWPProjectConfigurator.UpdateManifest(Path.Combine(path, "Package.appxmanifest"), FakeApps[0], "publisher", null);
            var appPackagesFolderX64 = Directory.CreateDirectory(Path.Combine(path, "AppPackages", "x64"));
            var appPackagesFolderArm64 = Directory.CreateDirectory(Path.Combine(path, "AppPackages", "arm64"));

            await File.WriteAllTextAsync(Path.Combine(appPackagesFolderX64.FullName, "test_x64.msix"), string.Empty);
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolderArm64.FullName, "test_arm64.msix"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "publish",
                    path,
                    "--verbose"
                });

            result.Should().Contain("Submission commit success! Here is some data:");
            result.Should().Contain("test_x64.msix");
            result.Should().Contain("test_arm64.msix");
        }

        [TestMethod]
        public async Task PublishCommandForFlutterAppsShouldCallFlutter()
        {
            var path = CopyFilesRecursively("FlutterProject");

            await FlutterProjectConfigurator.UpdateManifestAsync(
                new DirectoryInfo(path),
                new FileInfo(Path.Combine(path, "pubspec.yaml")),
                FakeApps[0],
                "publisher",
                null,
                null,
                null,
                CancellationToken.None);
            var appPackagesFolder = Directory.CreateDirectory(Path.Combine(path, "build", "windows", "runner", "Release"));
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolder.FullName, "test.msix"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "publish",
                    path,
                    "--verbose"
                });

            result.Should().Contain("Submission commit success! Here is some data:");
            result.Should().Contain("test.msix");
        }

        [TestMethod]
        [DataRow("Npm")]
        [DataRow("Yarn")]
        public async Task PublishCommandForElectronAppsShouldCallElectron(string manifestType)
        {
            var path = CopyFilesRecursively(Path.Combine("ElectronProject", manifestType));

            await ElectronProjectConfigurator.UpdateManifestAsync(
                new FileInfo(Path.Combine(path, "package.json")),
                FakeApps[0],
                "publisher",
                null,
                ElectronManifestManager.Object,
                CancellationToken.None);
            var appPackagesFolder = Directory.CreateDirectory(Path.Combine(path, "dist"));
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolder.FullName, "test.appx"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

            var dirInfo = new DirectoryInfo(path);

            if (manifestType == "Npm")
            {
                SetupNpmListReactNative(dirInfo, false);
            }
            else
            {
                SetupYarnListReactNative(dirInfo, false);
            }

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "publish",
                    path,
                    "--verbose"
                });

            result.Should().Contain("Submission commit success! Here is some data:");
            result.Should().Contain("test.appx");
        }

        [TestMethod]
        [DataRow("Npm")]
        [DataRow("Yarn")]
        public async Task PublishCommandForReactNativeAppsShouldUploadAppxUpload(string manifestType)
        {
            var path = CopyFilesRecursively(Path.Combine("ReactNativeProject", manifestType));

            var appxManifest = ReactNativeProjectConfigurator.GetAppXManifest(new DirectoryInfo(path));

            UWPProjectConfigurator.UpdateManifest(appxManifest.FullName, FakeApps[0], "publisher", null);

            var appPackagesFolder = Directory.CreateDirectory(Path.Combine(appxManifest.Directory!.FullName, "AppPackages"));
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolder.FullName, "test.appxupload"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

            var dirInfo = new DirectoryInfo(path);

            if (manifestType == "Npm")
            {
                SetupNpmListReactNative(dirInfo, true);
            }
            else
            {
                SetupYarnListReactNative(dirInfo, true);
            }

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "publish",
                    path,
                    "--verbose"
                });

            result.Should().Contain("Submission commit success! Here is some data:");
            result.Should().Contain("test.appxupload");
        }
    }
}