// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.API.Models
{
    public class StoreConfigurations
    {
        public int? SellerId { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? ClientId { get; set; }
    }
}
