// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MSStore.API.Packaged.Models
{
    public class PagedResponse<T>
    {
        [JsonPropertyName("@nextLink")]
        public string? NextLink { get; set; }
        public List<T>? Value { get; set; }
        public int TotalCount { get; set; }
    }
}