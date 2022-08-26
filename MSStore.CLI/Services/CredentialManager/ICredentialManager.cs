// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services.CredentialManager
{
    internal interface ICredentialManager
    {
        string? ReadCredential(string userName);
        void WriteCredential(string userName, string secret);
        void ClearCredentials(string userName);
    }
}
