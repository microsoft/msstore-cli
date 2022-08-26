// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.API.Models
{
    public class IsSystemFeatureRequired
    {
        public bool IsRequired { get; set; }
        public bool IsRecommended { get; set; }
        public string? HardwareItemType { get; set; }
    }
}
