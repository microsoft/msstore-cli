// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.CLI.Services.PartnerCenter
{
    internal class AccountEnrollments
    {
        public int TotalCount { get; set; }
        public List<AccountEnrollment>? Items { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }
}
