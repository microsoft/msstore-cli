// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class InitCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
        }

        [TestMethod]
        public async Task InitCommandShouldUseDefaultDirectoryIfNoArgument()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "init"
                ], -1);

            result.Error.Should().Contain($"We could not find a project configurator for the project at '{Directory.GetCurrentDirectory()}'.");
        }

        [TestMethod]
        public async Task InitCommandShouldOpenBrowserIfNotRegistered()
        {
            AddFakeAccount(null);

            var result = await ParseAndInvokeAsync(
                [
                    "init",
                    "https://www.microsoft.com/",
                    "--publish",
                    "--verbose"
                ], -2);

            result.Error.Should().Contain("I'll redirect you to the Microsoft Store Sign-up page.");

            BrowserLauncher.Verify(x => x.OpenBrowserAsync("https://partner.microsoft.com/dashboard/registration", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task InitCommandShouldFailIfAppIdIsNotFound()
        {
            AddDefaultFakeAccount();
            AddFakeApps();

            FakeStorePackagedAPI
                .Setup(x => x.GetApplicationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Not found"));

            var result = await ParseAndInvokeAsync(
                [
                    "init",
                    "https://www.microsoft.com/",
                    "--publish",
                    "--appId",
                    "9PN3ABCDEFGZ",
                    "--verbose"
                ], -1);

            result.Error.Should().Contain("Could not retrieve your application. Please make sure you have the correct AppId.");

            FakeStorePackagedAPI.Verify(x => x.GetApplicationsAsync(It.IsAny<CancellationToken>()), Times.Never);
            FakeConsole.Verify(
                x => x.SelectionPromptAsync(
                    It.Is<string>(s => s == "Which application should we use to configure your project?"),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<Func<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task InitCommandShouldFailOnCIIfNoAppIdIsProvided()
        {
            AddDefaultFakeAccount();
            AddFakeApps();

            EnvironmentInformationService
                .Setup(x => x.IsRunningOnCI)
                .Returns(true);

            var result = await ParseAndInvokeAsync(
                [
                    "init",
                    "https://www.microsoft.com/",
                    "--publish",
                    "--verbose"
                ], -1);

            result.Error.Should().Contain("Could not select an application because the current environment is not interactive.");
            result.Error.Should().Contain("--appId");

            FakeConsole.Verify(
                x => x.SelectionPromptAsync(
                    It.Is<string>(s => s == "Which application should we use to configure your project?"),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<Func<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task InitCommandShouldFailIfNotOnCIAndPromptIsNotSupported()
        {
            AddDefaultFakeAccount();
            AddFakeApps();

            FakeConsole
                .Setup(x => x.SelectionPromptAsync(
                    It.Is<string>(s => s == "Which application should we use to configure your project?"),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<Func<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException("Cannot show selection prompt since the current terminal isn't interactive."));

            var result = await ParseAndInvokeAsync(
                [
                    "init",
                    "https://www.microsoft.com/",
                    "--publish",
                    "--verbose"
                ], -1);

            result.Error.Should().Contain("Could not select an application because the current environment is not interactive.");
            result.Error.Should().Contain("--appId");

            FakeConsole.Verify(
                x => x.SelectionPromptAsync(
                    It.Is<string>(s => s == "Which application should we use to configure your project?"),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<Func<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}