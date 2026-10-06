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
        public string? CertificateThumbprint { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CertificateFilePath { get; set; }
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

        /// <summary>
        /// Gets or sets the region of the Azure AI Translator resource. Required for regional
        /// and multi-service resources, and unnecessary for a global one. This is not a
        /// secret; the key itself lives in the OS secure store.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TranslatorRegion { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ClientAssertion { get; set; }

        public StoreConfigurations GetStoreConfigurations() => new()
        {
            SellerId = SellerId,
            ClientId = ClientId,
            TenantId = TenantId
        };
    }
}
