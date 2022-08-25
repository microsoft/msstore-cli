// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using MSStore.CLI.UnitTests.Fakes;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ReconfigureCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public async Task Init()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);
        }

        [TestMethod]
        public async Task ReconfigureCommandWithCredentialsShouldReturnZero()
        {
            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "reconfigure"
                },
                () =>
                {
                    AddDefaultFakeAccount();

                    FakeConsole.AddNextFake("y");
                    FakeConsole.AddNextFake("y");

                    FakeConsole.AddNextFake("ClientId");
                    FakeConsole.AddNextFake("ClientSecret");

                    return Task.CompletedTask;
                });

            result.Should().Contain("Awesome! It seems to be working!");
        }

        [TestMethod]
        public async Task ReconfigureCommandShouldReturnZero()
        {
            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "reconfigure"
                },
                () =>
                {
                    AddDefaultFakeAccount();

                    FakeConsole.AddNextFake("y");
                    FakeConsole.AddNextFake("n");

                    FakeConsole.AddNextFake("ENTER");

                    FakeConsole.AddNextFake("y");

                    return Task.CompletedTask;
                });

            result.Should().Contain("Awesome! It seems to be working!");
        }
    }
}