// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class StatusDetails
    {
        public List<CodeAndDetail>? Errors { get; set; }
        public List<CodeAndDetail>? Warnings { get; set; }
        public List<CertificationReport>? CertificationReports { get; set; }
    }
}
