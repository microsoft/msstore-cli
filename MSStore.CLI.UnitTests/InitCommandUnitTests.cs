// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using MSStore.CLI.UnitTests.Fakes;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class InitCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public async Task Init()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);
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
            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "init",
                    "https://www.microsoft.com/",
                    "--verbose"
                },
                () =>
                {
                    AddFakeAccount(null);

                    return Task.CompletedTask;
                });

            result.Should().Contain("I'll redirect you to the Microsoft Store Sign-up page.");

            FakeBrowserLauncher.OpennedUrls.Should().Contain("https://partner.microsoft.com/dashboard/registration");
        }
    }
}