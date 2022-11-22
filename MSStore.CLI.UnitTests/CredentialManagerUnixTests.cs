// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using MSStore.CLI.Services.CredentialManager.Unix;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class CredentialManagerUnixTests
    {
        private CredentialManagerUnix _credentialManagerUnix = null!;

        [TestInitialize]
        public void Initialize()
        {
            _credentialManagerUnix = new CredentialManagerUnix();
            _credentialManagerUnix.ClearCredentials("testUserName");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _credentialManagerUnix.ClearCredentials("testUserName");
        }

        [TestMethod]
        public void CredentialManagerUnix_ReadCredential_ShouldReturnEmpty()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on non-Windows platforms");
            }

            var secret = _credentialManagerUnix.ReadCredential("testUserName");

            secret.Should().BeEmpty();
        }

        [TestMethod]
        public void CredentialManagerUnix_WriteCredential_ShouldPersist()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("This test is only valid on non-Windows platforms");
            }

            _credentialManagerUnix.WriteCredential("testUserName", "testSecret");

            var secret = _credentialManagerUnix.ReadCredential("testUserName");

            secret.Should().Be("testSecret");
        }
    }
}
