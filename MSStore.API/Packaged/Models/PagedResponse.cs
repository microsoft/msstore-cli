// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MSStore.API.Packaged.Models
{
    public class PagedResponse<T>
    {
        /// <summary>
        /// Gets or sets the relative link to the next page, or null on the last page.
        /// </summary>
        /// <remarks>
        /// The services send this as "@nextLink", which no naming policy produces from the
        /// property name, so it is mapped explicitly. Without it the value was always null.
        /// </remarks>
        [JsonPropertyName("@nextLink")]
        public string? NextLink { get; set; }

        public List<T>? Value { get; set; }
        public int TotalCount { get; set; }
    }
}