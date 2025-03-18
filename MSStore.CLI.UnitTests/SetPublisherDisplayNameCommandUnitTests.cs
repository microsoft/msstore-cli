// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services;
using MSStore.CLI.Services.PWABuilder;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class SetPublisherDisplayNameCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin("Test Publisher Display Name");
            AddDefaultFakeAccount();
            AddFakeApps();

            PWAAppInfoManager
                .Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PWAAppInfo
                {
                    AppId = FakeApps[0].Id,
                    Uri = new Uri("https://www.microsoft.com")
                });

            PartnerCenterManager
                .Setup(x => x.Enabled)
                .Returns(false);
        }

        [TestMethod]
        public async Task InitCommandUsesPublisherDisplayNameFromSettings()
        {
            var publisherDisplayName = "Test Publisher Display Name";

            var initResult = await ParseAndInvokeAsync(
                [
                                "init",
                                "https://microsoft.com",
                                "--publish",
                                "--verbose"
                ], -1);

            initResult.Error.Should().Contain($"Using PublisherDisplayName: {publisherDisplayName}");
        }

        [TestMethod]
        public async Task SetPublisherDisplayNameCommandShouldSaveSettings()
        {
            var publisherDisplayName = "New Test Publisher Display Name";

            await ParseAndInvokeAsync(
                [
                    "settings",
                    "setpdn",
                    publisherDisplayName
                ]);

            FakeConfigurationManager
                .Verify(x => x.SaveAsync(It.Is<Configurations>(c => c.PublisherDisplayName == publisherDisplayName), It.IsAny<CancellationToken>()));
        }
    }
}