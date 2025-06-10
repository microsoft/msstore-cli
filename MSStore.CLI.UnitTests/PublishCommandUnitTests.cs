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
        public async Task PublishCommandShouldUseDefaultDirectoryIfNoArgument()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "publish"
                ], -1);

            result.Error.Should().Contain($"We could not find a project publisher for the project at '{Directory.GetCurrentDirectory()}'.");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public async Task PublishCommandForUWPAppsShouldCallMSBuildIfWindows()
        {
            var path = CopyFilesRecursively("UWPProject");

            DefaultMSBuildExecution(new DirectoryInfo(path));

            AppXManifestManager.Object.UpdateManifest(Path.Combine(path, "Package.appxmanifest"), FakeApps[0], "publisher", null);
            var appPackagesFolder = Directory.CreateDirectory(Path.Combine(path, "AppPackages"));
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolder.FullName, "test.msixupload"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

            var result = await ParseAndInvokeAsync(
                [
                    "publish",
                    path,
                    "--verbose"
                ]);

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            result.Error.Should().Contain("test.msixupload");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public async Task PublishCommandForWinUIAppsShouldCallMSBuildIfWindows()
        {
            var path = CopyFilesRecursively("WinUIProject");

            var dirInfo = new DirectoryInfo(path);
            DefaultMSBuildExecution(dirInfo);
            SetupWinUI(dirInfo);

            AppXManifestManager.Object.UpdateManifest(Path.Combine(path, "Package.appxmanifest"), FakeApps[0], "publisher", null);
            var appPackagesFolderX64 = Directory.CreateDirectory(Path.Combine(path, "AppPackages", "x64"));
            var appPackagesFolderArm64 = Directory.CreateDirectory(Path.Combine(path, "AppPackages", "arm64"));

            await File.WriteAllTextAsync(Path.Combine(appPackagesFolderX64.FullName, "test_x64.msix"), string.Empty);
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolderArm64.FullName, "test_arm64.msix"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

            var result = await ParseAndInvokeAsync(
                [
                    "publish",
                    path,
                    "--verbose"
                ]);

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            result.Error.Should().Contain("test_x64.msix");
            result.Error.Should().Contain("test_arm64.msix");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public async Task PublishCommandForMauiAppsShouldCallMSBuildIfWindows()
        {
            var path = CopyFilesRecursively("MauiProject");

            var dirInfo = new DirectoryInfo(path);
            DefaultDotnetRestoreExecution(dirInfo);
            SetupWinUI(dirInfo);
            SetupMaui(dirInfo.GetFiles("*.csproj").First());

            AppXManifestManager.Object.MinimalUpdateManifest(Path.Combine(path, "Platforms", "Windows", "Package.appxmanifest"), FakeApps[0], "publisher");
            MauiProjectConfigurator.UpdateCSProj(new FileInfo(Path.Combine(path, "MauiApp.csproj")), FakeApps[0]);
            var appPackagesFolderX64 = Directory.CreateDirectory(Path.Combine(path, "AppPackages", "x64"));
            var appPackagesFolderArm64 = Directory.CreateDirectory(Path.Combine(path, "AppPackages", "arm64"));

            await File.WriteAllTextAsync(Path.Combine(appPackagesFolderX64.FullName, "test_x64.msix"), string.Empty);
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolderArm64.FullName, "test_arm64.msix"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

            var result = await ParseAndInvokeAsync(
                [
                    "publish",
                    path,
                    "--verbose"
                ]);

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            result.Error.Should().Contain("test_x64.msix");
            result.Error.Should().Contain("test_arm64.msix");
        }

        [TestMethod]
        public async Task PublishCommandForFlutterAppsShouldCallFlutter()
        {
            var path = CopyFilesRecursively("FlutterProject");

            await FlutterProjectConfigurator.UpdateManifestAsync(
                ErrorAnsiConsole,
                new DirectoryInfo(path),
                new FileInfo(Path.Combine(path, "pubspec.yaml")),
                FakeApps[0],
                "publisher",
                null,
                null,
                null,
                CancellationToken.None);
            var appPackagesFolder = Directory.CreateDirectory(Path.Combine(path, "build", "windows", "x64", "runner", "Release"));
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolder.FullName, "test.msix"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

            var result = await ParseAndInvokeAsync(
                [
                    "publish",
                    path,
                    "--verbose"
                ]);

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            result.Error.Should().Contain("test.msix");
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
                [
                    "publish",
                    path,
                    "--verbose"
                ]);

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            result.Error.Should().Contain("test.appx");
        }

        [TestMethod]
        [DataRow("Npm")]
        [DataRow("Yarn")]
        public async Task PublishCommandForReactNativeAppsShouldUploadAppxUpload(string manifestType)
        {
            var path = CopyFilesRecursively(Path.Combine("ReactNativeProject", manifestType));

            var appxManifest = FileProjectConfigurator.GetAppXManifest(new DirectoryInfo(path));

            AppXManifestManager.Object.UpdateManifest(appxManifest.FullName, FakeApps[0], "publisher", null);

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
                [
                    "publish",
                    path,
                    "--verbose"
                ]);

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            result.Error.Should().Contain("test.appxupload");
        }

        [TestMethod]
        public async Task PublishCommandForMSIXAppsShouldSucceed()
        {
            var path = CopyFilesRecursively("MSIXProject");

            var msixPath = Path.Combine(path, "test.msix");

            AddDefaultFakeSuccessfulSubmission();

            var result = await ParseAndInvokeAsync(
                [
                    "publish",
                    msixPath,
                    "--appId",
                    FakeApps[0].Id!,
                    "--verbose"
                ]);

            ZipFileManager
                .Verify(x => x.ExtractZip(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            result.Error.Should().Contain("test.msix");
        }

        [TestMethod]
        public async Task PublishCommandForMSIXAppsWithNoCommitShouldNotCommit()
        {
            var path = CopyFilesRecursively("MSIXProject");

            var msixPath = Path.Combine(path, "test.msix");

            AddDefaultFakeSuccessfulSubmission();

            var result = await ParseAndInvokeAsync(
                [
                    "publish",
                    msixPath,
                    "--appId",
                    FakeApps[0].Id!,
                    "--verbose",
                    "--noCommit"
                ]);

            ZipFileManager
                .Verify(x => x.ExtractZip(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            result.Error.Should().Contain("Skipping submission commit.");
        }

        [TestMethod]
        public async Task PublishCommandShouldSucceedForFlights()
        {
            var path = CopyFilesRecursively("MSIXProject");

            var msixPath = Path.Combine(path, "test.msix");

            AddFakeFlights();
            AddDefaultFakeSuccessfulFlightSubmission();

            var result = await ParseAndInvokeAsync(
                [
                    "publish",
                    msixPath,
                    "--appId",
                    FakeApps[0].Id!,
                    "--flightId",
                    FakeFlights[0].FlightId!,
                    "--verbose"
                ]);

            ZipFileManager
                .Verify(x => x.ExtractZip(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            result.Error.Should().Contain("test.msix");
        }
    }
}