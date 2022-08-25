// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class AppsCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public async Task Init()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);
        }

        [TestMethod]
        public async Task AppsListCommandShouldReturnZero()
        {
            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "apps",
                    "list"
                },
                () =>
                {
                    AddDefaultFakeAccount();
                    AddFakeApps();

                    return Task.CompletedTask;
                });

            result.Should().ContainAll(FakeApps.Select(a => a.Id));
            result.Should().ContainAll(FakeApps.Select(a => a.PrimaryName));
        }

        [TestMethod]
        public async Task AppsGetCommandShouldReturnZeroIfExistingApp()
        {
            var appId = FakeApps[2].Id!;
            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "apps",
                    "get",
                    appId
                },
                () =>
                {
                    AddDefaultFakeAccount();
                    AddFakeApps();

                    return Task.CompletedTask;
                });

            result.Should().Contain($"\"Id\": \"{appId}\",");
        }

        [TestMethod]
        public async Task AppsGetCommandShouldReturnErrorIfNonExistingApp()
        {
            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "apps",
                    "get",
                    "9PN3ABCDEFGD"
                },
                () =>
                {
                    AddDefaultFakeAccount();
                    AddFakeApps();

                    return Task.CompletedTask;
                },
                -1);

            result.Should().Contain("Error!");
        }
    }
}