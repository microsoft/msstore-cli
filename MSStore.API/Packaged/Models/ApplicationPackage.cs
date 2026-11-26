// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MSStore.API.Packaged.Models
{
    public class ApplicationPackage
    {
        public string? FileName { get; set; }
        public FileStatus? FileStatus { get; set; }
        public string? Id { get; set; }
        public string? Version { get; set; }
        public string? Architecture { get; set; }
        public List<string>? Languages { get; set; }
        public List<string>? Capabilities { get; set; }
        public string? MinimumDirectXVersion { get; set; }
        public string? MinimumSystemRam { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? TargetDeviceFamilies { get; set; }
    }
}
