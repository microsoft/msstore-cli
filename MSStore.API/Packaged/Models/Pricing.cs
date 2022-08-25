// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class Pricing
    {
        public string? TrialPeriod { get; set; }
        public Dictionary<string, string>? MarketSpecificPricings { get; set; }
        public string? PriceId { get; set; }
        public bool IsAdvancedPricingModel { get; set; }
    }
}
