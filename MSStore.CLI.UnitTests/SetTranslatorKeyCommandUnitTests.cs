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
        public async Task SetTranslatorKeyShouldStoreTheKeyInTheSecureStore()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "my-translator-key"
                ]);

            result.Error.Should().Contain("stored");

            CredentialManager.Verify(
                x => x.WriteCredential(AzureAITranslatorService.CredentialKeyName, "my-translator-key"),
                Times.Once);
        }

        [TestMethod]
        public async Task SetTranslatorKeyShouldTrimTheKeyAndRegionBeforePersisting()
        {
            // Whitespace around a pasted value is not valid in a request header, so it must
            // never reach the secure store or settings.json.
            Configurations? saved = null;
            FakeConfigurationManager
                .Setup(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .Callback((Configurations c, CancellationToken ct) => saved = c)
                .Returns(Task.CompletedTask);

            await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key",
                    "  my-translator-key\t",
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
            var result = await ParseAndInvokeAsync(
                [
                    "settings",
                    "set-translator-key"
                ],
                -1);

            result.Error.Should().Contain("A key is required.");
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
    }
}
