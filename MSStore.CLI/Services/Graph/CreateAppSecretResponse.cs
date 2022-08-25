// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.CLI.Services.Graph
{
    internal class CreateAppSecretResponse
    {
        public string? CustomKeyIdentifier { get; set; }
        public DateTime EndDateTime { get; set; }
        public string? KeyId { get; set; }
        public DateTime StartDateTime { get; set; }
        public string? SecretText { get; set; }
        public string? Hint { get; set; }
        public string? DisplayName { get; set; }
    }
}
