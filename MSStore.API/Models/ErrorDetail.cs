// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class ErrorDetail
    {
        public string? ErrorScenario { get; set; }
        public List<ErrorScenarioDetail>? ErrorScenarioDetails { get; set; }
    }
}
