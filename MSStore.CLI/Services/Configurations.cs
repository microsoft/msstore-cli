// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using MSStore.API.Models;

namespace MSStore.CLI.Services
{
    internal class Configurations
    {
        public int? SellerId { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? ClientId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StoreApiServiceUrl { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StoreApiScope { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DevCenterServiceUrl { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DevCenterScope { get; set; }

        // To be removed when PartnerCenterManager.Enabled == true
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PublisherDisplayName { get; set; }

        public StoreConfigurations GetStoreConfigurations() => new()
        {
            SellerId = SellerId,
            ClientId = ClientId,
            TenantId = TenantId
        };
    }
}
