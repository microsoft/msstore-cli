// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.API.Packaged.Models
{
    public class PackageDeliveryOptions
    {
        public PackageRollout? PackageRollout { get; set; }
        public bool IsMandatoryUpdate { get; set; }
        public DateTime? MandatoryUpdateEffectiveDate { get; set; }
    }
}
