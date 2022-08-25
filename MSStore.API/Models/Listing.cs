// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class Listing
    {
        public string? Language { get; set; }
        public string? Description { get; set; }
        public List<string>? ProductFeatures { get; set; }
        public List<string>? SearchTerms { get; set; }
        public string? AdditionalLicenseTerms { get; set; }
        public List<SystemRequirementDetail>? Requirements { get; set; }
    }
}
