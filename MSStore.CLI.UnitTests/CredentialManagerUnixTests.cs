// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void CredentialManagerUnix_ReadCredential_ShouldReturnEmpty()
        {
            var secret = _credentialManagerUnix.ReadCredential("testUserName");

            secret.Should().BeEmpty();
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void CredentialManagerUnix_WriteCredential_ShouldPersist()
        {
            _credentialManagerUnix.WriteCredential("testUserName", "testSecret");

            var secret = _credentialManagerUnix.ReadCredential("testUserName");

            secret.Should().Be("testSecret");
        }
    }
}
