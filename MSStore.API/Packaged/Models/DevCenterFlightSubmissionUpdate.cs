// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class DevCenterFlightSubmissionUpdate
    {
        public List<FlightPackageUpdate>? FlightPackages { get; set; }
        public PackageDeliveryOptions? PackageDeliveryOptions { get; set; }
        public string? TargetPublishMode { get; set; }
        public DateTime? TargetPublishDate { get; set; }
        public string? NotesForCertification { get; set; }
    }
}
