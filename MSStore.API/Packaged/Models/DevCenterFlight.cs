// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class DevCenterFlight
    {
        public string? FlightId { get; set; }
        public string? FriendlyName { get; set; }
        public ApplicationSubmissionInfo? LastPublishedFlightSubmission { get; set; }
        public ApplicationSubmissionInfo? PendingFlightSubmission { get; set; }
        public List<string>? GroupIds { get; set; }
        public string? RankHigherThan { get; set; }
    }
}