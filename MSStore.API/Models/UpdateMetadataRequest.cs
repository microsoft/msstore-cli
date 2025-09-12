// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class UpdateMetadataRequest
    {
        public Availability? Availability { get; set; }
        public Properties? Properties { get; set; }
        public Listing? Listings { get; set; }
        public List<string>? ListingsToAdd { get; set; }
        public List<string>? ListingsToRemove { get; set; }
    }
}
