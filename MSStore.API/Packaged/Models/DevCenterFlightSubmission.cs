// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class DevCenterFlightSubmission : IDevCenterSubmission
    {
        public string? Id { get; set; }
        public string? FlightId { get; set; }
        public string? Status { get; set; }
        public StatusDetails? StatusDetails { get; set; }
        public List<ApplicationPackage>? FlightPackages { get; set; }
        public PackageDeliveryOptions? PackageDeliveryOptions { get; set; }
        public string? FileUploadUrl { get; set; }
        public string? TargetPublishMode { get; set; }
        public DateTime? TargetPublishDate { get; set; }
        public string? NotesForCertification { get; set; }
    }
}
