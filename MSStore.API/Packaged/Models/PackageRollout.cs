// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.API.Packaged.Models
{
    public class PackageRollout
    {
        public bool IsPackageRollout { get; set; }
        public float PackageRolloutPercentage { get; set; }
        public string? PackageRolloutStatus { get; set; }
        public string? FallbackSubmissionId { get; set; }
    }
}
