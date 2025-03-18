// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class AppsCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddDefaultFakeAccount();
            AddFakeApps();
        }

        [TestMethod]
        public async Task AppsListCommandShouldReturnZero()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "apps",
                    "list"
                ]);

            result.Output.Should().ContainAll(FakeApps.Select(a => a.Id));
            result.Output.Should().ContainAll(FakeApps.Select(a => a.PrimaryName));
        }

        [TestMethod]
        public async Task AppsGetCommandShouldReturnZeroIfExistingApp()
        {
            var appId = FakeApps[2].Id!;
            var result = await ParseAndInvokeAsync(
                [
                    "apps",
                    "get",
                    appId
                ]);

            result.Output.Should().Contain($"\"Id\": \"{appId}\",");
        }

        [TestMethod]
        public async Task AppsGetCommandIsNotSupportedForUnpackagedApps()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "apps",
                    "get",
                    Guid.Empty.ToString()
                ], -1);

            result.Error.Should().Contain("This command is not supported for unpackaged applications.");
        }

        [TestMethod]
        public async Task AppsGetCommandShouldReturnErrorIfNonExistingApp()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "apps",
                    "get",
                    "9PN3ABCDEFGD"
                ],
                -1);

            result.Error.Should().Contain("Error!");
        }
    }
}