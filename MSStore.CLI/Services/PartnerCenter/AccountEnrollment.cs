// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.CLI.Services.PartnerCenter
{
    internal class AccountEnrollment
    {
        public string? Id { get; set; }
        public string? Cid { get; set; }
        public string? Status { get; set; }
        public string? AccountType { get; set; }
        public string? TypeName { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }
}
