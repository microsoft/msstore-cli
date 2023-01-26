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

            var dirInfo = new DirectoryInfo(path);

            DefaultMSBuildExecution(dirInfo);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s =>
                        s.Contains("/p:Configuration=Release;AppxBundle=Always;Platform=X64;AppxBundlePlatforms=\"X64|ARM64\"")
                        && s.EndsWith("UapAppxPackageBuildMode=StoreUpload)")),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"{Environment.NewLine}...{Environment.NewLine}abc -> {Path.Combine(path, "TestFile.msixupload")}{Environment.NewLine}",
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

            var dirInfo = new DirectoryInfo(path);

            DefaultMSBuildExecution(dirInfo);

            var customPath = Path.Combine(Path.GetTempPath(), "CustomPath");

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s =>
                        s.Contains("/p:Configuration=Release;AppxBundle=Always;Platform=X64;AppxBundlePlatforms=\"X64|ARM64\"")
                        && s.Contains($"AppxPackageDir={customPath}\\;")
                        && s.EndsWith("UapAppxPackageBuildMode=StoreUpload)")),
                    It.Is<string>(s => s == dirInfo.FullName),
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

        [TestMethod]
        public async Task PackageCommandForWinUIAppsShouldCallMSBuildIfWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on Windows platforms");
            }

            var path = CopyFilesRecursively("WinUIProject");

            var dirInfo = new DirectoryInfo(path);

            DefaultMSBuildExecution(dirInfo);
            SetupWinUI(dirInfo);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s =>
                        s.Contains("/p:Configuration=Release;AppxBundle=Always;Platform=X64;AppxBundlePlatforms=X64")
                        && s.Contains("UapAppxPackageBuildMode=StoreUpload")),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"{Environment.NewLine}...{Environment.NewLine}abc -> {Path.Combine(path, "TestFile_x64.msix")}{Environment.NewLine}",
                    StdErr = string.Empty
                });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s =>
                        s.Contains("/p:Configuration=Release;AppxBundle=Always;Platform=ARM64;AppxBundlePlatforms=ARM64")
                        && s.Contains("UapAppxPackageBuildMode=StoreUpload")),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"{Environment.NewLine}...{Environment.NewLine}abc -> {Path.Combine(path, "TestFile_arm64.msix")}{Environment.NewLine}",
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
        public async Task PackageCommandForWinUIAppsShouldCallMSBuildWithOutputParameterIfWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on Windows platforms");
            }

            var path = CopyFilesRecursively("WinUIProject");

            var dirInfo = new DirectoryInfo(path);

            DefaultMSBuildExecution(dirInfo);
            SetupWinUI(dirInfo);

            var customPath = Path.Combine(Path.GetTempPath(), "CustomPath");

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s =>
                        s.Contains("/p:Configuration=Release;AppxBundle=Always;Platform=X64;AppxBundlePlatforms=X64")
                        && s.Contains($"AppxPackageDir={customPath}\\;")
                        && s.EndsWith($"AppxPackageTestDir={customPath}\\WinUIProject_1.0.0.0_X64_Test\\)")
                        && s.Contains("UapAppxPackageBuildMode=StoreUpload")),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"{Environment.NewLine}...{Environment.NewLine}abc -> {Path.Combine(customPath, "x64", "TestFile_x64.msix")}{Environment.NewLine}",
                    StdErr = string.Empty
                });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s =>
                        s.Contains("/p:Configuration=Release;AppxBundle=Always;Platform=ARM64;AppxBundlePlatforms=ARM64")
                        && s.Contains($"AppxPackageDir={customPath}\\;")
                        && s.EndsWith($"AppxPackageTestDir={customPath}\\WinUIProject_1.0.0.0_ARM64_Test\\)")
                        && s.Contains("UapAppxPackageBuildMode=StoreUpload")),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"{Environment.NewLine}...{Environment.NewLine}abc -> {Path.Combine(customPath, "arm64", "TestFile_arm64.msix")}{Environment.NewLine}",
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

        [TestMethod]
        [DataRow(-1, "WinUIProject")]
        [DataRow(-1, "UWPProject")]
        [DataRow(-6, "FlutterProject")]
        [DataRow(-6, "ElectronProject", "Npm")]
        [DataRow(-6, "ElectronProject", "Yarn")]
        [DataRow(-6, "ReactNativeProject", "Npm")]
        [DataRow(-6, "ReactNativeProject", "Yarn")]
        public async Task PackageCommandShouldNotWorkIfNotWindowsOnSpecificPlatforms(int expectedResult, params string[] testDataProjectSubPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on non-Windows platforms");
            }

            var path = CopyFilesRecursively(Path.Combine(testDataProjectSubPath));

            var dirInfo = new DirectoryInfo(path);

            SetupBasedOnTestDataProjectSubPath(dirInfo, testDataProjectSubPath);

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "package",
                    path,
                    "--verbose"
                }, expectedResult);

            if (expectedResult == -6)
            {
                result.Should().Contain("This project type can only be packaged on Windows.");
            }
        }

        [TestMethod]
        public async Task PackageCommandForFlutterAppsShouldCallFlutter()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on Windows platforms");
            }

            var path = CopyFilesRecursively("FlutterProject");

            var dirInfo = new DirectoryInfo(path);

            SetupPubGet(dirInfo);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub run msix:build --store"),
                    It.Is<string>(s => s == dirInfo.FullName),
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
                    It.Is<string>(s => s == dirInfo.FullName),
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on Windows platforms");
            }

            var path = CopyFilesRecursively("FlutterProject");

            var dirInfo = new DirectoryInfo(path);

            SetupPubGet(dirInfo);

            var customPath = Path.Combine(Path.GetTempPath(), "CustomPath");

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == $"pub run msix:build --store --output-path \"{customPath}\""),
                    It.Is<string>(s => s == dirInfo.FullName),
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
                    It.Is<string>(s => s == dirInfo.FullName),
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

        private void SetupPubGet(DirectoryInfo dirInfo)
        {
            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub get"),
                    It.Is<string>(s => s == dirInfo.FullName),
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on Windows platforms");
            }

            var path = CopyFilesRecursively(Path.Combine("ElectronProject", "Npm"));

            var dirInfo = new DirectoryInfo(path);

            SetupNpmListReactNative(dirInfo, false);

            SetupNpmInstall(dirInfo);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "npx"),
                    It.Is<string>(s => s == "electron-builder build -w=appx --x64 --arm64"),
                    It.Is<string>(s => s == dirInfo.FullName),
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on Windows platforms");
            }

            var path = CopyFilesRecursively(Path.Combine("ElectronProject", "Yarn"));

            var dirInfo = new DirectoryInfo(path);

            SetupYarnListReactNative(dirInfo, false);

            SetupYarnInstall(dirInfo);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "run electron-builder build -w=appx --x64 --arm64"),
                    It.Is<string>(s => s == dirInfo.FullName),
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
        [DataRow("Npm")]
        [DataRow("Yarn")]
        public async Task PackageCommandForReactNativeNpmAppsShouldCallMSBuild(string manifestType)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on Windows platforms");
            }

            var path = CopyFilesRecursively(Path.Combine("ReactNativeProject", manifestType));

            var dirInfo = new DirectoryInfo(path);

            if (manifestType == "Npm")
            {
                SetupNpmListReactNative(dirInfo, true);
                SetupNpmInstall(dirInfo);
            }
            else
            {
                SetupYarnListReactNative(dirInfo, true);
                SetupYarnInstall(dirInfo);
            }

            var windowsDir = new DirectoryInfo(Path.Combine(path, "windows"));

            DefaultMSBuildExecution(windowsDir);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s =>
                        s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s =>
                        s.Contains("/p:Configuration=Release;AppxBundle=Always;Platform=X64;AppxBundlePlatforms=\"X64|ARM64\"")
                        && s.EndsWith("UapAppxPackageBuildMode=StoreUpload)")),
                    It.Is<string>(s => s == windowsDir.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = $"{Environment.NewLine}...{Environment.NewLine}abc -> {Path.Combine(path, "TestFile.msixupload")}{Environment.NewLine}",
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
    }
}