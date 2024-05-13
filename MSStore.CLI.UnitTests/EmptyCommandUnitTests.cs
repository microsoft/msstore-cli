// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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

            result.Should().Contain("CLI tool to automate Microsoft Store Developer tasks.");
        }

        [TestMethod]
        public async Task EmptyCommandShouldReturnZeroIfNotSignedIn()
        {
            FakeConsole
                .Setup(x => x.YesNoConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var clientId = "3F0BCAEF-6334-48CF-837F-81CB0F1F2C45";
            var secret = "ClientSecret";

            FakeConsole
                .SetupSequence(x => x.RequestStringAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(clientId)
                .ReturnsAsync(secret);

            AddDefaultFakeAccount();

            AddDefaultGraphOrg();

            var result = await ParseAndInvokeAsync(Array.Empty<string>());

            result.Should().Contain("Awesome! It seems to be working!");
        }

        [TestMethod]
        public async Task InfoCommandShouldReturnZero()
        {
            FakeLogin();

            var result = await ParseAndInvokeAsync(new[] { "info" });

            result.Should().Contain("Current Config");
        }

        [TestMethod]
        public async Task InfoCommandShouldReturnZeroWithCert()
        {
            FakeLoginWithCert();

            var result = await ParseAndInvokeAsync(new[] { "info" });

            result.Should().Contain("Current Config");
        }
    }
}