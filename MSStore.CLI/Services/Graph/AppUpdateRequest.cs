// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.CLI.Services.Graph
{
    internal class AppUpdateRequest
    {
        public List<string>? IdentifierUris { get; set; }
        public string? SignInAudience { get; set; }
    }
}
