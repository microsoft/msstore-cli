// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class EmptyCommandUnitTests : BaseCommandLineTest
    {
        [TestMethod]
        public async Task EmptyCommandShouldReturnZeroIfLoggedIn()
        {
            FakeLogin();

            var result = await ParseAndInvokeAsync(Array.Empty<string>());

            result.Should().Contain("CLI tool to automate Microsoft Store tasks.");
        }

        [TestMethod]
        public async Task EmptyCommandShouldReturnZeroIfNotSignedIn()
        {
            var result = await ParseAndInvokeAsync(
                Array.Empty<string>(),
                async () =>
                {
                    AddDefaultFakeAccount();

                    var clientId = "ClientId";
                    var secret = "ClientSecret";

                    await FakeStoreAPIFactory.InitAsync(
                        new API.Models.Configurations
                        {
                            SellerId = 1,
                            TenantId = "1",
                            ClientId = clientId
                        },
                        secret,
                        CancellationToken.None);

                    FakeConsole.AddNextFake("y");

                    FakeConsole.AddNextFake(clientId);
                    FakeConsole.AddNextFake(secret);
                });

            result.Should().Contain("Awesome! It seems to be working!");
        }

        [TestMethod]
        public async Task InfoCommandShouldReturnZero()
        {
            FakeLogin();

            var result = await ParseAndInvokeAsync(new[] { "info" });

            result.Should().Contain("Current config:");
        }
    }
}