// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.CredentialManager.Windows;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class CredentialManagerWindowsTests
    {
        private const string TestApplicationName = "MicrosoftStoreCliUnitTests";

        private CredentialManagerWindows _credentialManagerWindows = null!;

        [TestInitialize]
        public void Initialize()
        {
            _credentialManagerWindows = new CredentialManagerWindows
            {
                ApplicationName = TestApplicationName
            };
            _credentialManagerWindows.ClearCredentials("testUserName");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _credentialManagerWindows.ClearCredentials("testUserName");
        }

        [TestMethod]
        public void CredentialManagerWindows_ReadCredential_ShouldReturnEmpty()
        {
            var secret = _credentialManagerWindows.ReadCredential("testUserName");

            secret.Should().BeEmpty();
        }

        [TestMethod]
        public void CredentialManagerWindows_WriteCredential_ShouldPersist()
        {
            _credentialManagerWindows.WriteCredential("testUserName", "testSecret");

            var secret = _credentialManagerWindows.ReadCredential("testUserName");

            secret.Should().Be("testSecret");
        }
    }
}
