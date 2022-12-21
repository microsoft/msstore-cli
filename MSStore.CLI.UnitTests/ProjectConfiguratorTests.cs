// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.PWABuilder;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ProjectConfiguratorTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddDefaultFakeAccount();
            AddFakeApps();
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesFlutterProject()
        {
            var path = CopyFilesRecursively("FlutterProject");

            var dirInfo = new DirectoryInfo(path);

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

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub add --dev msix --dry-run"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "No dependencies would change",
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    path,
                    "--verbose"
                });

            result = result.Replace(Environment.NewLine, string.Empty);

            ExternalCommandExecutor.VerifyAll();

            result.Should().Contain("This seems to be a Flutter project.");
            result.Should().Contain("is now configured to build to the Microsoft Store!");

            var pubspecYamlFileContents = await File.ReadAllTextAsync(Path.Combine(path, "pubspec.yaml"));

            pubspecYamlFileContents.Should().Contain("msix");
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesAlreadyConfiguredFlutterProject()
        {
            var path = CopyFilesRecursively("FlutterProject");

            var dirInfo = new DirectoryInfo(path);

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

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub add --dev msix --dry-run"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "Would change XX dependencies.",
                    StdErr = string.Empty
                });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub add --dev msix"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    path,
                    "--verbose"
                });

            result = result.Replace(Environment.NewLine, string.Empty);

            ExternalCommandExecutor.VerifyAll();

            result.Should().Contain("This seems to be a Flutter project.");
            result.Should().Contain("is now configured to build to the Microsoft Store!");

            var pubspecYamlFileContents = await File.ReadAllTextAsync(Path.Combine(path, "pubspec.yaml"));

            pubspecYamlFileContents.Should().Contain("msix");
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesElectronNpmProject()
        {
            var path = CopyFilesRecursively(Path.Combine("ElectronProject", "Npm"));

            var dirInfo = new DirectoryInfo(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "npm"),
                    It.Is<string>(s => s == "install"),
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
                    It.Is<string>(s => s == "npm"),
                    It.Is<string>(s => s == "list electron-builder"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "`-- (empty)",
                    StdErr = string.Empty
                });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "npm"),
                    It.Is<string>(s => s == "install --save-dev electron-builder"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    path,
                    "--verbose"
                });

            result = result.Replace(Environment.NewLine, string.Empty);

            ExternalCommandExecutor.VerifyAll();

            result.Should().Contain("This seems to be a Electron project.");
            result.Should().Contain("is now configured to build to the Microsoft Store!");

            var packageJsonFileContents = await File.ReadAllTextAsync(Path.Combine(path, "package.json"));

            packageJsonFileContents.Should().Contain("appx");
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesAlreadyConfiguredElectronNpmProject()
        {
            var path = CopyFilesRecursively(Path.Combine("ElectronProject", "Npm"));

            var dirInfo = new DirectoryInfo(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "npm"),
                    It.Is<string>(s => s == "install"),
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
                    It.Is<string>(s => s == "npm"),
                    It.Is<string>(s => s == "list electron-builder"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "`-- electron-builder@23.6.0",
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    path,
                    "--verbose"
                });

            result = result.Replace(Environment.NewLine, string.Empty);

            ExternalCommandExecutor.VerifyAll();

            result.Should().Contain("This seems to be a Electron project.");
            result.Should().Contain("is now configured to build to the Microsoft Store!");

            var packageJsonFileContents = await File.ReadAllTextAsync(Path.Combine(path, "package.json"));

            packageJsonFileContents.Should().Contain("appx");
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesElectronYarnProject()
        {
            var path = CopyFilesRecursively(Path.Combine("ElectronProject", "Yarn"));

            var dirInfo = new DirectoryInfo(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "install"),
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
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "list --pattern electron-builder"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "Done in 0s.",
                    StdErr = string.Empty
                });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "add --dev electron-builder"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    path,
                    "--verbose"
                });

            result = result.Replace(Environment.NewLine, string.Empty);

            ExternalCommandExecutor.VerifyAll();

            result.Should().Contain("This seems to be a Electron project.");
            result.Should().Contain("is now configured to build to the Microsoft Store!");

            var packageJsonFileContents = await File.ReadAllTextAsync(Path.Combine(path, "package.json"));

            packageJsonFileContents.Should().Contain("appx");
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesAlreadyConfiguredElectronYarnProject()
        {
            var path = CopyFilesRecursively(Path.Combine("ElectronProject", "Yarn"));

            var dirInfo = new DirectoryInfo(path);

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "install"),
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
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "list --pattern electron-builder"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Services.ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = "└─ electron-builder@23.6.0",
                    StdErr = string.Empty
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    path,
                    "--verbose"
                });

            result = result.Replace(Environment.NewLine, string.Empty);

            ExternalCommandExecutor.VerifyAll();

            result.Should().Contain("This seems to be a Electron project.");
            result.Should().Contain("is now configured to build to the Microsoft Store!");

            var packageJsonFileContents = await File.ReadAllTextAsync(Path.Combine(path, "package.json"));

            packageJsonFileContents.Should().Contain("appx");
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesUWPProject()
        {
            var path = CopyFilesRecursively("UWPProject");

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    path,
                    "--verbose"
                });

            result = result.Replace(Environment.NewLine, string.Empty);

            result.Should().Contain("This seems to be a UWP project.");

            var appxManifestFileContents = await File.ReadAllTextAsync(Path.Combine(path, "Package.appxmanifest"));

            appxManifestFileContents.Should().Contain("Fake App");
        }

        private void SetupSuccessfullPWA()
        {
            FakeConsole
                .SetupSequence(x => x.RequestStringAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("1")
                .ReturnsAsync("en-US");

            PWABuilderClient
                .Setup(x => x.GenerateZipAsync(It.IsAny<GenerateZipRequest>(), It.IsAny<string>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
                .Returns((GenerateZipRequest generateZipRequest, string outputZipPath, IProgress<double> progress, CancellationToken ct) =>
                {
                    progress.Report(0);
                    progress.Report(100);

                    return Task.CompletedTask;
                });
            PWABuilderClient
                .Setup(x => x.FetchWebManifestAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebManifestFetchResponse
                {
                    Content = new WebManifestFetchContent
                    {
                        Json = new WebManifestJson
                        {
                            Description = "Test description",
                            Screenshots = new List<ScreenShot>
                            {
                                new ScreenShot
                                {
                                    Src = "https://www.microsoft.com/image1.png"
                                }
                            },
                            Icons = new List<Icon>
                            {
                                new Icon
                                {
                                    Src = "https://www.microsoft.com/image2.png",
                                    Sizes = "512x512"
                                },
                                new Icon
                                {
                                    Src = "https://www.microsoft.com/image3.png",
                                    Sizes = "6x5"
                                }
                            }
                        }
                    },
                });

            AddDefaultFakeSuccessfulSubmission();

            PWAAppInfoManager
                .Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PWAAppInfo
                {
                    AppId = FakeApps[0].Id,
                    Uri = new Uri("https://www.microsoft.com")
                });

            ZipFileManager
                .Setup(x => x.ExtractZip(It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string zipFile, string destination) =>
                {
                    var dir = Directory.CreateDirectory(destination);

                    // Create Fake File
                    File.WriteAllText(Path.Combine(dir.FullName, "FAKE.msix"), string.Empty);
                });
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesPWASuccessfullyIfPublish()
        {
            SetupSuccessfullPWA();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    "https://microsoft.com",
                    "--publish",
                    "--verbose"
                });

            TokenManager
                .Verify(x => x.SelectAccountAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);

            result.Should().Contain("You've provided a URL, so we'll use");
            result.Should().Contain("Submission commit success!");
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesPWASuccessfullyIfOutput()
        {
            SetupSuccessfullPWA();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    "https://microsoft.com",
                    "-o",
                    Path.GetTempPath()
                });

            result.Should().Contain("You've provided a URL, so we'll use");
            result.Should().NotContain("Submission commit success!");
        }

        [TestMethod]
        public async Task ProjectConfiguratorFailsToParsePWAIfNotPublishAndNotOutput()
        {
            SetupSuccessfullPWA();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    "https://microsoft.com"
                }, -2);

            result.Should().Contain("For PWAs the init command should output to a specific directory (using the '--output' option), or publish directly to the store using the '--publish' option.");
        }

        [TestMethod]
        public async Task ProjectConfiguratorParserPWAShouldNotCallPartnerCenterAPIIfPublisherNameIsProvided()
        {
            SetupSuccessfullPWA();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    "https://microsoft.com",
                    "--publisherDisplayName",
                    "FAKE_PUBLISHER_DISPLAY_NAME",
                    "--publish",
                    "--verbose"
                });

            TokenManager
                .Verify(x => x.SelectAccountAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

            result.Should().Contain("You've provided a URL, so we'll use");
            result.Should().Contain("Submission commit success!");
        }
    }
}
