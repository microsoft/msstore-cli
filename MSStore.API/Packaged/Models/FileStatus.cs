// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace MSStore.API.Packaged.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter<FileStatus>))]
    public enum FileStatus
    {
        None,
        PendingUpload,
        Uploaded,
        PendingDelete
    }
}