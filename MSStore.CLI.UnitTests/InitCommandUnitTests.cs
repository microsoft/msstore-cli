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
        [ExpectedException(typeof(ArgumentException))]
        public async Task InitCommandShouldFailIfNoArgument()
        {
            await ParseAndInvokeAsync(
                new string[]
                {
                    "init"
                });
        }

        [TestMethod]
        public async Task InitCommandShouldOpenBrowserIfNotRegistered()
        {
            AddFakeAccount(null);

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    "https://www.microsoft.com/",
                    "--publish",
                    "--verbose"
                }, -2);

            result.Should().Contain("I'll redirect you to the Microsoft Store Sign-up page.");

            BrowserLauncher.Verify(x => x.OpenBrowserAsync("https://partner.microsoft.com/dashboard/registration", true, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}