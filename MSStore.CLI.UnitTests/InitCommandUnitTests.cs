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
    }
}