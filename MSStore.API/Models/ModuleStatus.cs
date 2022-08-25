// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.API.Models
{
    public class ModuleStatus
    {
        public bool IsReady { get; set; }
        public string? OngoingSubmissionId { get; set; }
    }
}
