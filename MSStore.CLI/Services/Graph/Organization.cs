// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.CLI.Services.Graph
{
    internal class Organization
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public string? TenantType { get; set; }
        public List<VerifiedDomain>? VerifiedDomains { get; set; }
    }
}
