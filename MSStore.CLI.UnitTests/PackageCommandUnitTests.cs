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
                        s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s =>
                        s.Contains("/p:Configuration=Release;AppxBundle=Always;Platform=X64;AppxBundlePlatforms=\"X64|ARM64\"")
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

            var customPath = Path.Combine(Path.GetTempPath(), "CustomPath");

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s =>
                        s.Contains("/p:Configuration=Release;AppxBundle=Always;Platform=X64;AppxBundlePlatforms=\"X64|ARM64\"")
                        && s.Contains($"AppxPackageDir=\"{customPath}\"")
                        && s.EndsWith("UapAppxPackageBuildMode=StoreUpload)")),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"{Environment.NewLine}...{Environment.NewLine}abc -> {Path.Combine(customPath, "TestFile.msixupload")}{Environment.NewLine}",
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "package",
                    path,
                    "--output",
                    customPath,
                    "--verbose"
                });

            result.Should().Contain("The packaged app is here:");
            result.Should().Contain(customPath);

            ExternalCommandExecutor.VerifyAll();
        }

        private void DefaultMSBuildExecution(string path)
        {
            ExternalCommandExecutor
                            .Setup(x => x.RunAsync(
                                It.Is<string>(s =>
                                    s.Contains("vswhere.exe")),
                                It.Is<string>(s =>
                                    s.Contains("-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe")),
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
                    It.Is<string>(s => s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s => s.Contains("/t:restore")),
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
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub run msix:build --store"),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub run msix:pack --store"),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
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
            result.Should().Contain(path);
        }

        [TestMethod]
        public async Task PackageCommandForFlutterAppsShouldCallFlutterWithOutputParameter()
        {
            var path = CopyFilesRecursively("FlutterProject");
            SetupPubGet(path);

            var customPath = Path.Combine(Path.GetTempPath(), "CustomPath");

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == $"pub run msix:build --store --output-path \"{customPath}\""),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == $"pub run msix:pack --store --output-path \"{customPath}\""),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"msix created: {Path.Combine(customPath, "TestFile.msix")}",
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "package",
                    path,
                    "--output",
                    customPath,
                    "--verbose"
                });

            result.Should().Contain("The packaged app is here:");
            result.Should().Contain(customPath);
        }

        private void SetupPubGet(string path)
        {
            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub get"),
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
        public async Task PackageCommandForElectronNpmAppsShouldCallElectronNpm()
        {
            var path = CopyFilesRecursively(Path.Combine("ElectronProject", "Npm"));
            SetupNpmInstall(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "npx"),
                    It.Is<string>(s => s == "electron-builder build -w=appx --x64 --arm64"),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "target=AppX file=app.appx",
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
            result.Should().Contain(path);
        }

        [TestMethod]
        public async Task PackageCommandForElectronYarnAppsShouldCallElectronYarn()
        {
            var path = CopyFilesRecursively(Path.Combine("ElectronProject", "Yarn"));
            SetupYarnInstall(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "run electron-builder build -w=appx --x64 --arm64"),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "target=AppX file=app.appx",
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
            result.Should().Contain(path);
        }

        private void SetupNpmInstall(string path)
        {
            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "npm"),
                    It.Is<string>(s => s == "install"),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });
        }

        private void SetupYarnInstall(string path)
        {
            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "install"),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });
        }
    }
}