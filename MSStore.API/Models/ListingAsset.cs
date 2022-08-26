// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class ListingAsset
    {
        public string? Language { get; set; }
        public List<StoreLogo>? StoreLogos { get; set; }
        public List<Screenshot>? Screenshots { get; set; }
    }
}
