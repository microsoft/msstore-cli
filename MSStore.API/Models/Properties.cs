// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class Properties
    {
        public bool IsPrivacyPolicyRequired { get; set; }
        public string? PrivacyPolicyUrl { get; set; }
        public string? WebSite { get; set; }
        public string? SupportContactInfo { get; set; }
        public string? CertificationNotes { get; set; }
        public string? Category { get; set; }
        public string? SubCategory { get; set; }
        public ProductDeclarations? ProductDeclarations { get; set; }
        public List<IsSystemFeatureRequired>? IsSystemFeatureRequired { get; set; }
        public List<SystemRequirementDetail>? SystemRequirementDetails { get; set; }
    }
}
