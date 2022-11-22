// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class PackageCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddDefaultFakeAccount();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task PackageCommandShouldFailIfNoArgument()
        {
            await ParseAndInvokeAsync(
                new string[]
                {
                    "package"
                });
        }

        [TestMethod]
        public async Task PackageCommandForUWPAppsShouldCallMSBuildIfWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on Windows platforms");
            }

            var path = CopyFilesRecursively("UWPProject");
            DefaultMSBuildExecution(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\" /p:Configuration=Release;AppxBundle=Always;Platform=x64;AppxBundlePlatforms=\"x64|ARM64\"")
                        && s.EndsWith("UapAppxPackageBuildMode=StoreUpload)")),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"{Environment.NewLine}...{Environment.NewLine}abc -> TestFile.msixupload{Environment.NewLine}",
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "package",
                    path,
                    "--verbose"
                });

            ExternalCommandExecutor.VerifyAll();
        }

        [TestMethod]
        public async Task PackageCommandForUWPAppsShouldCallMSBuildWithOutputParameterIfWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on Windows platforms");
            }

            var path = CopyFilesRecursively("UWPProject");
            DefaultMSBuildExecution(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\" /p:Configuration=Release;AppxBundle=Always;Platform=x64;AppxBundlePlatforms=\"x64|ARM64\"")
                        && s.Contains("AppxPackageDir=\"C:\\CustomPath\"")
                        && s.EndsWith("UapAppxPackageBuildMode=StoreUpload)")),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"{Environment.NewLine}...{Environment.NewLine}abc -> C:\\CustomPath\\TestFile.msixupload{Environment.NewLine}",
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "package",
                    path,
                    "--output",
                    "C:\\CustomPath",
                    "--verbose"
                });

            result.Should().Contain("The packaged app is here:");
            result.Should().Contain("C:\\CustomPath\\TestFile.msix");

            ExternalCommandExecutor.VerifyAll();
        }

        private void DefaultMSBuildExecution(string path)
        {
            ExternalCommandExecutor
                            .Setup(x => x.RunAsync(
                                It.Is<string>(s =>
                                    s.Contains("vswhere.exe\" -latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe")),
                                It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                                It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new Services.ExternalCommandExecutionResult
                            {
                                ExitCode = 0,
                                StdOut = "MSBuild.exe",
                                StdErr = string.Empty
                            });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s.Contains("\"MSBuild.exe\" /t:restore")),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });
        }

        [TestMethod]
        public async Task PackageCommandForUWPAppsShouldNotWorkIfNotWindows()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on non-Windows platforms");
            }

            var path = CopyFilesRecursively("UWPProject");

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "package",
                    path,
                    "--verbose"
                }, -1);

            result.Should().Contain("Packaging UWP apps is only supported on Windows");
        }

        [TestMethod]
        public async Task PackageCommandForFlutterAppsShouldCallFlutter()
        {
            var path = CopyFilesRecursively("FlutterProject");
            SetupPubGet(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(It.Is<string>(s => s == "flutter pub run msix:build --store"), It.Is<string>(s => s == new DirectoryInfo(path).FullName), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(It.Is<string>(s => s == "flutter pub run msix:pack --store"), It.Is<string>(s => s == new DirectoryInfo(path).FullName), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "msix created: TestFile.msix",
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "package",
                    path,
                    "--verbose"
                });

            result.Should().Contain("The packaged app is here:");
            result.Should().Contain("TestFile.msix");
        }

        [TestMethod]
        public async Task PackageCommandForFlutterAppsShouldCallFlutterWithOutputParameter()
        {
            var path = CopyFilesRecursively("FlutterProject");
            SetupPubGet(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(It.Is<string>(s => s == "flutter pub run msix:build --store --output-path \"C:\\CustomPath\""), It.Is<string>(s => s == new DirectoryInfo(path).FullName), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(It.Is<string>(s => s == "flutter pub run msix:pack --store --output-path \"C:\\CustomPath\""), It.Is<string>(s => s == new DirectoryInfo(path).FullName), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "msix created: C:\\CustomPath\\TestFile.msix",
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "package",
                    path,
                    "--output",
                    "C:\\CustomPath",
                    "--verbose"
                });

            result.Should().Contain("The packaged app is here:");
            result.Should().Contain("C:\\CustomPath\\TestFile.msix");
        }

        private void SetupPubGet(string path)
        {
            ExternalCommandExecutor
                            .Setup(x => x.RunAsync(It.Is<string>(s => s == "flutter pub get"), It.Is<string>(s => s == new DirectoryInfo(path).FullName), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new Services.ExternalCommandExecutionResult
                            {
                                ExitCode = 0,
                                StdOut = string.Empty,
                                StdErr = string.Empty
                            });
        }
    }
}