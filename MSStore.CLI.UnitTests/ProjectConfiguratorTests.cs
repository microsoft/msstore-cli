// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using FluentAssertions;
using MSStore.CLI.Commands.Init.Setup;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ProjectConfiguratorTests : BaseCommandLineTest
    {
        [TestInitialize]
        public async Task Init()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);
        }

        private static string CopyFilesRecursively(string sourcePath, [CallerMemberName] string caller = null!)
        {
            sourcePath = Path.Combine("TestData", sourcePath);

            var targetPath = Path.Combine(caller, sourcePath);

            Directory.CreateDirectory(targetPath);

            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }

            return targetPath;
        }

        [TestMethod]
        public async Task ProjectConfiguratorParsesFlutterProject()
        {
            var path = CopyFilesRecursively("FlutterProject");

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    path,
                    "--verbose"
                },
                () =>
                {
                    AddDefaultFakeAccount();
                    AddFakeApps();

                    FakeExternalCommandExecutor.AddNextFake(new Services.ExternalCommandExecutionResult
                    {
                        ExitCode = 0,
                        StdOut = "No dependencies would change",
                        StdErr = string.Empty
                    });

                    return Task.CompletedTask;
                });

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
                },
                () =>
                {
                    AddDefaultFakeAccount();
                    AddFakeApps();

                    return Task.CompletedTask;
                });

            result.Should().Contain("This seems to be a UWP project.");

            var appxManifestFileContents = await File.ReadAllTextAsync(Path.Combine(path, "Package.appxmanifest"));

            appxManifestFileContents.Should().Contain("Fake App");
        }

        [TestMethod]
        public async Task ProjectConfiguratorParserPWA()
        {
            PWAProjectConfigurator.DefaultSubmissionPollDelay = TimeSpan.Zero;

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    "https://microsoft.com",
                    "--verbose"
                },
                () =>
                {
                    AddDefaultFakeAccount();
                    AddFakeApps();
                    AddDefaultFakeSubmission();
                    FakeStoreAPIFactory.FakeStorePackagedAPI.InitDefaultSubmissionStatusResponseQueue();

                    FakeConsole.AddNextFake("1");
                    FakeConsole.AddNextFake("en-us");

                    return Task.CompletedTask;
                });

            result.Should().Contain("You've provided a URL, so we'll use PWABuilder.com to setup your PWA and upload");
            result.Should().Contain("Submission commit success!");
        }
    }
}
