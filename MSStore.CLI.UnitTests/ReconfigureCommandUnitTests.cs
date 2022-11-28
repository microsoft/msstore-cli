// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.Graph;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ReconfigureCommandUnitTests : BaseCommandLineTest
    {
        [TestMethod]
        public async Task ReconfigureCommandWithCredentialsShouldReturnZero()
        {
            FakeLogin();

            AddDefaultGraphOrg();

            FakeConsole
                .SetupSequence(x => x.YesNoConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(true);

            FakeConsole
                .SetupSequence(x => x.RequestStringAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45")
                .ReturnsAsync("ClientSecret");

            AddDefaultFakeAccount();

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "reconfigure"
                });

            result.Should().Contain("Awesome! It seems to be working!");
        }

        [TestMethod]
        public async Task ReconfigureCommandShouldReturnZero()
        {
            FakeLogin();

            AddDefaultGraphOrg();

            FakeConsole
                .SetupSequence(x => x.YesNoConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false)
                .ReturnsAsync(true);

            FakeConsole
                .Setup(x => x.RequestStringAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("ENTER");

            AddDefaultFakeAccount();

            var fakeAppId = new Guid("184A3F40-0E02-4B71-A979-582051ECDD9F");

            GraphClient
                .Setup(x => x.CreateAppAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureApplication
                {
                    Id = "FakeId",
                    DisplayName = "displayName",
                    AppId = fakeAppId,
                });

            GraphClient
                .Setup(x => x.CreateAppSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateAppSecretResponse
                {
                    SecretText = $"clientIdFakeSecret",
                    DisplayName = "displayName",
                });

            GraphClient
                .Setup(x => x.CreatePrincipalAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreatePrincipalResponse
                {
                    Id = "Id",
                    AppId = fakeAppId
                });

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "reconfigure"
                });

            result.Should().Contain("Awesome! It seems to be working!");
        }

        [TestMethod]
        public async Task ReconfigureCommandWithCredentialsAndTenantShouldReturnZero()
        {
            AddDefaultGraphOrg();

            FakeConsole
                .SetupSequence(x => x.YesNoConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(true);

            FakeConsole
                .SetupSequence(x => x.RequestStringAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45")
                .ReturnsAsync("ClientSecret");

            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "reconfigure",
                    "--tenantId",
                    DefaultOrganization.Id!.Value.ToString(),
                    "--sellerId",
                    "12345"
                });

            TokenManager
                .Verify(x => x.SelectAccountAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            TokenManager
                .Verify(x => x.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);

            result.Should().Contain("Awesome! It seems to be working!");
        }

        [TestMethod]
        public async Task ReconfigureCommandWithAllInfoShouldReturnZero()
        {
            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "reconfigure",
                    "--tenantId",
                    DefaultOrganization.Id!.Value.ToString(),
                    "--sellerId",
                    "12345",
                    "--clientId",
                    "3F0BCAEF-6334-48CF-837F-81CB0F1F2C45",
                    "--clientSecret",
                    "ClientSecret",
                });

            TokenManager
                .Verify(x => x.SelectAccountAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            TokenManager
                .Verify(x => x.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);

            result.Should().Contain("Awesome! It seems to be working!");
        }
    }
}