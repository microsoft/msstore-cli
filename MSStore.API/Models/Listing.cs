// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class Listing
    {
        public string? Language { get; set; }
        public string? Description { get; set; }
        public string? WhatsNew { get; set; }
        public List<string>? ProductFeatures { get; set; }
        public string? ShortDescription { get; set; }
        public List<string>? SearchTerms { get; set; }
        public string? AdditionalLicenseTerms { get; set; }
        public string? Copyright { get; set; }
        public string? DevelopedBy { get; set; }
        public List<SystemRequirementDetail>? Requirements { get; set; }
        public string? SortTitle { get; set; }
        public string? ContactInfo { get; set; }
    }
}
