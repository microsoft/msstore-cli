// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.Telemetry;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class TelemetryUnitTests : BaseCommandLineTest
    {
        [TestMethod]
        public async Task TelemetryConfigurationLoadsAsync()
        {
            var telemetryConnectionStringProvider = await TelemetryConnectionStringProvider.LoadAsync(default);

            telemetryConnectionStringProvider.Should().NotBeNull();

            telemetryConnectionStringProvider?.AIConnectionString.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task EmptyCommandFirstRunShouldHavePrivacyLinkIfNotSignedIn()
        {
            AddDefaultGraphOrg();

            FakeConsole
                .Setup(x => x.YesNoConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await ParseAndInvokeAsync([], null);

            result.Error.Should().Contain("https://aka.ms/privacy");
        }

        [TestMethod]
        public async Task EmptyCommandFirstRunShouldHavePrivacyLinkIfSignedIn()
        {
            FakeLogin();

            var result = await ParseAndInvokeAsync([], null);

            result.Error.Should().Contain("https://aka.ms/privacy");
        }
    }
}
