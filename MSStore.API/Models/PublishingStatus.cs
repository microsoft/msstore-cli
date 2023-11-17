// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace MSStore.API.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter<PublishingStatus>))]
    public enum PublishingStatus
    {
        INPROGRESS,
        PUBLISHED,
        FAILED,
        UNKNOWN
    }
}
