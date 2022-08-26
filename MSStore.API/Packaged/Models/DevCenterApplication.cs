// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.API.Packaged.Models
{
    public class DevCenterApplication
    {
        public string? Id { get; set; }
        public string? PrimaryName { get; set; }
        public string? PackageFamilyName { get; set; }
        public string? PackageIdentityName { get; set; }
        public string? PublisherName { get; set; }
        public DateTime? FirstPublishedDate { get; set; }
        public ApplicationSubmissionInfo? PendingApplicationSubmission { get; set; }
        public bool HasAdvancedListingPermission { get; set; }
        public ApplicationSubmissionInfo? LastPublishedApplicationSubmission { get; set; }
    }
}