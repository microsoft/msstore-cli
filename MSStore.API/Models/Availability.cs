// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class Availability
    {
        public List<string>? Markets { get; set; }
        public string? Discoverability { get; set; }
        public bool EnableInFutureMarkets { get; set; }
        public string? Pricing { get; set; }
        public string? FreeTrial { get; set; }
    }
}
