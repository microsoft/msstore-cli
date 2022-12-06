// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Packaged.Models;
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

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub add --dev msix --dry-run"),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
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

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "flutter"),
                    It.Is<string>(s => s == "pub add --dev msix --dry-run"),
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
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
                    It.Is<string>(s => s == new DirectoryInfo(path).FullName),
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

            AddDefaultFakeSubmission();
            InitDefaultSubmissionStatusResponseQueue();

            FakeStorePackagedAPI
                .Setup(x => x.CommitSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevCenterCommitResponse
                {
                    Status = "CommitStarted",
                });

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

            result.Should().Contain("You've provided a URL, so we'll use PWABuilder.com to setup your PWA and upload");
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

            result.Should().Contain("You've provided a URL, so we'll use PWABuilder.com to setup your PWA and upload");
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

            result.Should().Contain("You've provided a URL, so we'll use PWABuilder.com to setup your PWA and upload");
            result.Should().Contain("Submission commit success!");
        }
    }
}
