// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.API.Models
{
    public class CreateSubmissionResponse
    {
        public string? PollingUrl { get; set; }
        public string? SubmissionId { get; set; }
        public string? OngoingSubmissionId { get; set; }
    }
}