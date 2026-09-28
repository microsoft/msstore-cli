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
    }
}
