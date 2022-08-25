// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services.Graph
{
    internal class AppPasswordRegistrationRequest
    {
        public string? DisplayName { get; set; }
        public string? EndDateTime { get; set; }
        public string? StartDateTime { get; set; }
    }
}
