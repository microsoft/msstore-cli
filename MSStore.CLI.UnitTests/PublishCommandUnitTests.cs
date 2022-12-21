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

            UWPProjectConfigurator.UpdateManifest(Path.Combine(path, "Package.appxmanifest"), FakeApps[0], "publisher");
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
        public async Task PublishCommandForFlutterAppsShouldCallFlutter()
        {
            var path = CopyFilesRecursively("FlutterProject");

            await FlutterProjectConfigurator.UpdateManifestAsync(
                new DirectoryInfo(path),
                new FileInfo(Path.Combine(path, "pubspec.yaml")),
                FakeApps[0],
                "publisher",
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
                ElectronManifestManager.Object,
                CancellationToken.None);
            var appPackagesFolder = Directory.CreateDirectory(Path.Combine(path, "dist"));
            await File.WriteAllTextAsync(Path.Combine(appPackagesFolder.FullName, "test.appx"), string.Empty);

            AddDefaultFakeSuccessfulSubmission();

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
    }
}