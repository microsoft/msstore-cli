// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.CLI.Services.Graph
{
    internal class AzureApplication
    {
        public string? Id { get; set; }
        public Guid? AppId { get; set; }
        public string? DisplayName { get; set; }
    }
}
