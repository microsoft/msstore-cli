// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API;
using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests
{
    /// <summary>
    /// Regression tests for the ordering bug where <c>reconfigure</c> mutated the OS credential store before
    /// validating the new configuration, so a failed reconfigure could permanently destroy a working client secret.
    /// </summary>
    [TestClass]
    public class ReconfigureCredentialSafetyUnitTests : BaseCommandLineTest
    {
        private const string ExistingClientId = "3F0BCAEF-6334-48CF-837F-81CB0F1F2C45";
        private const string ExistingSecret = "existingWorkingSecret";

        private readonly Dictionary<string, string> _credentialStore = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string?> _validationSecrets = [];
        private readonly List<Dictionary<string, string>> _credentialStoreDuringValidation = [];

        [TestInitialize]
        [TestCleanup]
        public void ClearAssertionVars()
        {
            Environment.SetEnvironmentVariable("MSSTORE_CLIENT_ASSERTION", null);
            Environment.SetEnvironmentVariable("MSSTORE_CLIENT_ASSERTION_FILE", null);
        }

        /// <summary>
        /// Replaces the list-based credential mock from <see cref="BaseCommandLineTest"/> with a dictionary-backed
        /// in-memory credential store seeded with a working client secret, so assertions can look at the actual
        /// state of the store rather than at a call log. Also captures the secret each validation attempt is made
        /// with, and a snapshot of the store at that moment.
        /// </summary>
        private void ArrangeExistingClientSecretConfiguration(bool validationSucceeds)
        {
            _credentialStore.Clear();
            _credentialStore[ExistingClientId] = ExistingSecret;

            CredentialManager.Reset();
            CredentialManager
                .Setup(x => x.ReadCredential(It.IsAny<string>()))
                .Returns((string userName) => _credentialStore.TryGetValue(userName, out var secret) ? secret : string.Empty);
            CredentialManager
                .Setup(x => x.WriteCredential(It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string userName, string secret) => _credentialStore[userName] = secret);
            CredentialManager
                .Setup(x => x.ClearCredentials(It.IsAny<string>()))
                .Callback((string userName) => _credentialStore.Remove(userName));

            FakeStoreAPIFactory
                .Setup(fac => fac.CreateWithSecretAsync(It.IsAny<Configurations>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback((Configurations config, string? secret, CancellationToken ct) =>
                {
                    _validationSecrets.Add(secret);
                    _credentialStoreDuringValidation.Add(new Dictionary<string, string>(_credentialStore, StringComparer.OrdinalIgnoreCase));
                })
                .Returns(() => validationSucceeds
                    ? Task.FromResult(FakeStoreAPI.Object)
                    : Task.FromException<IStoreAPI>(new InvalidOperationException("Invalid credential")));
        }

        private Task<(string Output, string Error)> ReconfigureAsync(string[] credentialArgs, int expectedResult)
        {
            return ParseAndInvokeAsync(
                [
                    "reconfigure",
                    "--tenantId",
                    DefaultOrganization.Id!.Value.ToString(),
                    "--sellerId",
                    "12345",
                    "--clientId",
                    ExistingClientId,
                    .. credentialArgs
                ],
                expectedResult);
        }

        private void VerifyCredentialStoreWasNotTouched(bool saveAttempted = false)
        {
            _credentialStore.Should().ContainKey(ExistingClientId);
            _credentialStore[ExistingClientId].Should().Be(ExistingSecret);

            CredentialManager.Verify(x => x.WriteCredential(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            CredentialManager.Verify(x => x.ClearCredentials(It.IsAny<string>()), Times.Never);

            FakeConfigurationManager.Verify(
                x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()),
                saveAttempted ? Times.Once() : Times.Never());
        }

        [TestMethod]
        public async Task FailedClientAssertionReconfigureShouldNotDestroyExistingClientSecret()
        {
            // The exact repro from the issue: a working client-secret configuration switched to client assertion
            // mode without MSSTORE_CLIENT_ASSERTION set. Neither a client secret nor a certificate password is
            // supplied, which used to reach ClearCredentials before anything had been validated.
            ArrangeExistingClientSecretConfiguration(validationSucceeds: false);

            await ReconfigureAsync(["--clientAssertion"], expectedResult: -1);

            VerifyCredentialStoreWasNotTouched();
        }

        [TestMethod]
        public async Task FailedCertificateThumbprintReconfigureShouldNotDestroyExistingClientSecret()
        {
            // --certificateThumbprint reached the same branch, so the bug was never client-assertion specific.
            ArrangeExistingClientSecretConfiguration(validationSucceeds: false);

            await ReconfigureAsync(["--certificateThumbprint", "abc"], expectedResult: -1);

            VerifyCredentialStoreWasNotTouched();
        }

        [TestMethod]
        public async Task FailedClientSecretReconfigureShouldNotOverwriteExistingClientSecret()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: false);

            await ReconfigureAsync(["--clientSecret", "brandNewSecret"], expectedResult: -1);

            VerifyCredentialStoreWasNotTouched();
        }

        [TestMethod]
        public async Task FailedCertificateFileReconfigureShouldNotDestroyExistingClientSecret()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: false);

            await ReconfigureAsync(["--certificateFilePath", "C:\\x.pfx", "--certificatePassword", "certPassword"], expectedResult: -1);

            VerifyCredentialStoreWasNotTouched();
        }

        [TestMethod]
        public async Task SaveFailureShouldNotOverwriteExistingClientSecret()
        {
            // Validation passes, but persisting settings.json fails. Writing the certificate password would
            // overwrite - and permanently destroy - the client secret the saved configuration still refers to.
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);
            FakeConfigurationManager
                .Setup(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("settings.json is locked"));

            await ReconfigureAsync(["--certificateFilePath", "C:\\x.pfx", "--certificatePassword", "certPassword"], expectedResult: -1);

            VerifyCredentialStoreWasNotTouched(saveAttempted: true);
        }

        [TestMethod]
        public async Task SaveFailureShouldNotClearExistingClientSecret()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);
            FakeConfigurationManager
                .Setup(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("settings.json is locked"));

            await ReconfigureAsync(["--clientAssertion"], expectedResult: -1);

            VerifyCredentialStoreWasNotTouched(saveAttempted: true);
        }

        [TestMethod]
        public async Task SilentlyFailedCredentialClearShouldNotReportSuccess()
        {
            // ClearCredentials cannot report failure on any platform, so a credential can survive the clear. The
            // saved configuration would then be validated with a null PKCS#12 password but run with the stale
            // secret as the password, so reconfigure must not claim success.
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);
            CredentialManager
                .Setup(x => x.ClearCredentials(It.IsAny<string>()))
                .Callback(() => { });

            var result = await ReconfigureAsync(["--certificateFilePath", "C:\\x.pfx"], expectedResult: -1);

            result.Error.Should().NotContain("Awesome! It seems to be working!");
            result.Error.Should().Contain("could not be removed");

            _credentialStore[ExistingClientId].Should().Be(ExistingSecret);
            FakeConfigurationManager.Verify(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SuccessfulCredentialClearShouldBeVerifiedAndReportSuccess()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);

            var result = await ReconfigureAsync(["--certificateFilePath", "C:\\x.pfx"], expectedResult: 0);

            // The clear is confirmed by reading the credential back, rather than assumed to have worked.
            // Guid.ToString() is lower-case, so match the client ID case-insensitively.
            CredentialManager.Verify(
                x => x.ReadCredential(It.Is<string>(s => s.Equals(ExistingClientId, StringComparison.OrdinalIgnoreCase))),
                Times.Once);
            result.Error.Should().Contain("Awesome! It seems to be working!");
        }

        [TestMethod]
        public async Task ReconfigureShouldValidateBeforeMutatingTheCredentialStore()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);

            await ReconfigureAsync(["--clientSecret", "brandNewSecret"], expectedResult: 0);

            // The store still held the old secret while the candidate configuration was being validated, proving
            // validation no longer needs the new credential to be staged in the store first.
            _credentialStoreDuringValidation.Should().ContainSingle();
            _credentialStoreDuringValidation[0][ExistingClientId].Should().Be(ExistingSecret);

            // And validation never consults the store at all - the candidate secret is passed in explicitly.
            CredentialManager.Verify(x => x.ReadCredential(It.IsAny<string>()), Times.Never);

            _credentialStore[ExistingClientId].Should().Be("brandNewSecret");
        }

        [TestMethod]
        public async Task SuccessfulClientSecretReconfigureShouldStoreTheSecret()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);

            await ReconfigureAsync(["--clientSecret", "brandNewSecret"], expectedResult: 0);

            _validationSecrets.Should().ContainSingle().Which.Should().Be("brandNewSecret");
            _credentialStore[ExistingClientId].Should().Be("brandNewSecret");

            CredentialManager.Verify(x => x.ClearCredentials(It.IsAny<string>()), Times.Never);
            FakeConfigurationManager.Verify(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SuccessfulCertificatePasswordReconfigureShouldStoreThePassword()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);

            await ReconfigureAsync(["--certificateFilePath", "C:\\x.pfx", "--certificatePassword", "certPassword"], expectedResult: 0);

            // The certificate password doubles as the PKCS#12 password used to load the certificate file.
            _validationSecrets.Should().ContainSingle().Which.Should().Be("certPassword");
            _credentialStore[ExistingClientId].Should().Be("certPassword");

            CredentialManager.Verify(x => x.ClearCredentials(It.IsAny<string>()), Times.Never);
            FakeConfigurationManager.Verify(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task PasswordLessCertificateFileReconfigureShouldNotValidateWithTheStaleSecret()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);

            await ReconfigureAsync(["--certificateFilePath", "C:\\x.pfx"], expectedResult: 0);

            // A password-less certificate file must be loaded with a null PKCS#12 password. Simply deferring the
            // credential clear would have left the previous client secret in the store, where it would have been
            // silently used as the certificate password instead.
            _validationSecrets.Should().ContainSingle().Which.Should().BeNull();

            _credentialStore.Should().NotContainKey(ExistingClientId);
            FakeConfigurationManager.Verify(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SuccessfulClientAssertionReconfigureShouldClearTheObsoleteSecret()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);

            await ReconfigureAsync(["--clientAssertion"], expectedResult: 0);

            _validationSecrets.Should().ContainSingle().Which.Should().BeNull();

            // Once the client-assertion configuration is validated and saved, the client secret is obsolete.
            _credentialStore.Should().NotContainKey(ExistingClientId);
            FakeConfigurationManager.Verify(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SuccessfulCertificateThumbprintReconfigureShouldClearTheObsoleteSecret()
        {
            ArrangeExistingClientSecretConfiguration(validationSucceeds: true);

            await ReconfigureAsync(["--certificateThumbprint", "abc"], expectedResult: 0);

            _validationSecrets.Should().ContainSingle().Which.Should().BeNull();

            _credentialStore.Should().NotContainKey(ExistingClientId);
            FakeConfigurationManager.Verify(x => x.SaveAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
