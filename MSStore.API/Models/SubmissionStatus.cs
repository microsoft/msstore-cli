// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.API.Models
{
    public class SubmissionStatus
    {
        public PublishingStatus PublishingStatus { get; set; }
        public bool HasFailed { get; set; }
    }
}
