// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.CLI.Services.Graph
{
    internal class ListResponse<T>
    {
        public List<T>? Value { get; set; }
    }
}
