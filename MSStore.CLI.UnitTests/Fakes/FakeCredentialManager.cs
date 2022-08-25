// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.CredentialManager;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeCredentialManager : ICredentialManager
    {
        private string? _userName;
        private string? _secret;

        public void ClearCredentials(string userName)
        {
            _userName = string.Empty;
        }

        public string? ReadCredential(string userName)
        {
            if (_userName == userName)
            {
                return _secret;
            }

            return string.Empty;
        }

        public void WriteCredential(string userName, string secret)
        {
            _userName = userName;
            _secret = secret;
        }
    }
}
