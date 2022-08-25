// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using Meziantou.Framework.Win32;

namespace MSStore.CLI.Services.CredentialManager.Windows
{
    internal class CredentialManagerWindows : ICredentialManager
    {
        internal string ApplicationName { get; set; } = "MicrosoftStoreCli";

        private string GetCredentialName(string userName) => $"{ApplicationName}:user={userName}";

        public string? ReadCredential(string userName)
        {
            var clientCredential = Meziantou.Framework.Win32.CredentialManager.ReadCredential(GetCredentialName(userName));

            if (clientCredential != null)
            {
                return clientCredential.Password;
            }

            return string.Empty;
        }

        public void WriteCredential(string userName, string secret)
        {
            Meziantou.Framework.Win32.CredentialManager.WriteCredential(GetCredentialName(userName), userName, secret, CredentialPersistence.LocalMachine);
        }

        public void ClearCredentials(string userName)
        {
            try
            {
                var credentialName = GetCredentialName(userName);
                if (Meziantou.Framework.Win32.CredentialManager.EnumerateCredentials(credentialName).Any())
                {
                    Meziantou.Framework.Win32.CredentialManager.DeleteCredential(credentialName);
                }
            }
            catch
            {
            }
        }
    }
}
