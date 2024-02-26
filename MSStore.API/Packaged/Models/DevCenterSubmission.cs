// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class DevCenterSubmission : IDevCenterSubmission
    {
        public string? Id { get; set; }
        public DevCenterApplicationCategory? ApplicationCategory { get; set; }
        public Pricing? Pricing { get; set; }
        public string? Visibility { get; set; }
        public string? TargetPublishMode { get; set; }
        public DateTime? TargetPublishDate { get; set; }
        public Dictionary<string, DevCenterListing>? Listings { get; set; }
        public List<string>? HardwarePreferences { get; set; }
        public bool AutomaticBackupEnabled { get; set; }
        public bool CanInstallOnRemovableMedia { get; set; }
        public bool IsGameDvrEnabled { get; set; }
        public List<GamingOption>? GamingOptions { get; set; }
        public bool HasExternalInAppProducts { get; set; }
        public bool MeetAccessibilityGuidelines { get; set; }
        public string? NotesForCertification { get; set; }
        public string? Status { get; set; }
        public StatusDetails? StatusDetails { get; set; }
        public string? FileUploadUrl { get; set; }
        public List<ApplicationPackage>? ApplicationPackages { get; set; }
        public PackageDeliveryOptions? PackageDeliveryOptions { get; set; }
        public string? EnterpriseLicensing { get; set; }
        public bool AllowMicrosoftDecideAppAvailabilityToFutureDeviceFamilies { get; set; }
        public Dictionary<string, bool>? AllowTargetFutureDeviceFamilies { get; set; }
        public string? FriendlyName { get; set; }
        public List<Trailer>? Trailers { get; set; }
    }
}
