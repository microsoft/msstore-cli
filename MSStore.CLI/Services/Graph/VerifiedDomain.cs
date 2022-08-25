// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services.Graph
{
    internal class VerifiedDomain
    {
        public string? Capabilities { get; set; }
        public bool IsDefault { get; set; }
        public bool IsInitial { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
    }
}