// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services;
using MSStore.CLI.Services.Translation;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class SetTranslatorKeyCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddDefaultFakeAccount();
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldPromptForTheKeyWithoutEchoingIt()
        {
            FakeConsole
                .Setup(x => x.RequestStringAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync("my-translator-key");

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key"
                ]);

            result.Error.Should().Contain("stored");

            FakeConsole.Verify(
                x => x.RequestStringAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
                Times.Once);
            CredentialManager.Verify(
                x => x.WriteCredential(AzureAITranslatorService.CredentialKeyName, "my-translator-key"),
                Times.Once);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldNotAcceptTheKeyAsAnArgument()
        {
            // Anything on the command line is recorded in shell history and visible in process
            // listings, so the key must never be accepted there.
            await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "my-translator-key"
                ],
                1);

            CredentialManager.Verify(
                x => x.WriteCredential(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldReadTheKeyFromStandardInput()
        {
            FakeConsole
                .Setup(x => x.IsInputRedirected)
                .Returns(true);
            FakeConsole
                .Setup(x => x.ReadAllStandardInputAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("my-translator-key\n");

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "--key-stdin"
                ]);

            result.Error.Should().Contain("stored");

            // The user has stated a key is coming, so the read must not give up on a slow producer.
            FakeConsole.Verify(
                x => x.ReadAllStandardInputAsync(It.Is<TimeSpan?>(t => t == null), It.IsAny<CancellationToken>()),
                Times.Once);
            FakeConsole.Verify(
                x => x.RequestStringAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Never);
            CredentialManager.Verify(
                x => x.WriteCredential(AzureAITranslatorService.CredentialKeyName, "my-translator-key"),
                Times.Once);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldRejectKeyStdinWhenInputIsNotRedirected()
        {
            // Reading an interactive console to its end would appear to hang.
            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "--key-stdin"
                ],
                -1);

            result.Error.Should().Contain("standard input is not redirected");

            FakeConsole.Verify(
                x => x.ReadAllStandardInputAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldNotPromptWhenInputIsRedirected()
        {
            FakeConsole
                .Setup(x => x.IsInputRedirected)
                .Returns(true);

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key"
                ],
                -1);

            result.Error.Should().Contain("Cannot prompt for the key");
            result.Error.Should().Contain("--key-stdin");

            FakeConsole.Verify(
                x => x.RequestStringAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldNotPromptOnCI()
        {
            EnvironmentInformationService
                .Setup(x => x.IsRunningOnCI)
                .Returns(true);

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key"
                ],
                -1);

            result.Error.Should().Contain("Cannot prompt for the key");
            result.Error.Should().Contain(AzureAITranslatorService.KeyEnvironmentVariable);

            FakeConsole.Verify(
                x => x.RequestStringAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldTrimTheKeyAndRegionBeforePersisting()
        {
            // Whitespace around a pasted value is not valid in a request header, so it must
            // never reach the secure store or settings.json.
            FakeConsole
                .Setup(x => x.RequestStringAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync("  my-translator-key\t");

            Configurations? saved = null;
            FakeConfigurationManager
                .Setup(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .Callback((Configurations c, CancellationToken ct) => saved = c)
                .Returns(Task.CompletedTask);

            await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "--region",
                    "  westus2  "
                ]);

            CredentialManager.Verify(
                x => x.WriteCredential(AzureAITranslatorService.CredentialKeyName, "my-translator-key"),
                Times.Once);

            saved.Should().NotBeNull();
            saved!.TranslatorRegion.Should().Be("westus2");
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldRequireAKey()
        {
            FakeConsole
                .Setup(x => x.RequestStringAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync("   ");

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key"
                ],
                -1);

            result.Error.Should().Contain("A key is required.");

            CredentialManager.Verify(
                x => x.WriteCredential(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldClearStoredValues()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "--clear"
                ]);

            result.Error.Should().Contain("cleared");

            CredentialManager.Verify(
                x => x.ClearCredentials(AzureAITranslatorService.CredentialKeyName),
                Times.Once);
        }

        [TestMethod]
        public async Task SetTranslatorKeyClearShouldReportAKeyThatSurvivesTheClear()
        {
            // ClearCredentials is best-effort on every platform and can fail silently, so a key
            // that is still readable afterwards must be reported rather than called "cleared".
            CredentialManager
                .Setup(x => x.ClearCredentials(It.IsAny<string>()));
            CredentialManager
                .Setup(x => x.ReadCredential(AzureAITranslatorService.CredentialKeyName))
                .Returns("still-here");

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "--clear"
                ],
                -1);

            result.Error.Should().Contain("could not be removed from the credential store");
            result.Error.Should().NotContain("cleared.");

            // Nothing else is changed when the key could not be removed.
            FakeConfigurationManager.Verify(
                x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldNotOverwriteTheKeyWhenSavingSettingsFails()
        {
            // settings.json is saved first, so a failure there leaves the working key in place.
            FakeConsole
                .Setup(x => x.RequestStringAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync("new-key");
            FakeConfigurationManager
                .Setup(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("The disk is full."));

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "--region",
                    "westus2"
                ],
                -1);

            result.Error.Should().Contain("Could not store the Azure AI Translator key.");

            CredentialManager.Verify(
                x => x.WriteCredential(AzureAITranslatorService.CredentialKeyName, It.IsAny<string>()),
                Times.Never);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldRestoreTheRegionWhenTheKeyCannotBeStored()
        {
            // The region is saved before the key, so a key that then fails to store must not
            // leave the new region paired with the old key.
            UseStoredRegion("eastus");
            var savedRegions = CaptureSavedRegions();
            FakeConsole
                .Setup(x => x.RequestStringAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync("new-key");
            CredentialManager
                .Setup(x => x.WriteCredential(AzureAITranslatorService.CredentialKeyName, It.IsAny<string>()))
                .Throws(new InvalidOperationException("The keyring is unavailable."));

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "--region",
                    "westus2"
                ],
                -1);

            result.Error.Should().Contain("Could not store the Azure AI Translator key.");
            savedRegions.Should().Equal("westus2", "eastus");
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldSayWhenTheRegionCouldNotBeRestored()
        {
            UseStoredRegion("eastus");
            var saves = 0;
            FakeConfigurationManager
                .Setup(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .Returns(() => ++saves == 1 ? Task.CompletedTask : Task.FromException(new IOException("The disk is full.")));
            FakeConsole
                .Setup(x => x.RequestStringAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync("new-key");
            CredentialManager
                .Setup(x => x.WriteCredential(AzureAITranslatorService.CredentialKeyName, It.IsAny<string>()))
                .Throws(new InvalidOperationException("The keyring is unavailable."));

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "--region",
                    "westus2"
                ],
                -1);

            result.Error.Should().Contain("The region was saved, but the Azure AI Translator key could not be stored.");
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldSayWhenAnEarlierRegionIsKept()
        {
            // A new key may be for a different resource, so reusing the stored region is
            // reported rather than done silently.
            UseStoredRegion("westus2");
            FakeConsole
                .Setup(x => x.RequestStringAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync("new-key");

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key"
                ]);

            result.Error.Should().Contain("Using the region 'westus2' stored earlier.");
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldSanitizeTheEchoedRegion()
        {
            // The stored region can come from a hand-edited settings.json.
            UseStoredRegion("west\u0007us2");
            FakeConsole
                .Setup(x => x.RequestStringAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync("new-key");

            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key"
                ]);

            result.Error.Should().NotContain("\u0007");
            result.Error.Should().Contain("Using the region 'west us2' stored earlier.");
        }

        private void UseStoredRegion(string region)
        {
            FakeConfigurationManager
                .Setup(x => x.LoadAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Configurations
                {
                    SellerId = 1,
                    TenantId = new Guid("41261775-DB6D-4B44-9A36-7EB8565C7D22"),
                    ClientId = new Guid("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45"),
                    TranslatorRegion = region
                });
        }

        private List<string?> CaptureSavedRegions()
        {
            var regions = new List<string?>();
            FakeConfigurationManager
                .Setup(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .Callback((Configurations c, CancellationToken ct) => regions.Add(c.TranslatorRegion))
                .Returns(Task.CompletedTask);
            return regions;
        }
    }
}
