// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.API.Packaged.Models
{
    public class FlightPackageUpdate
    {
        public string? Id { get; set; }
        public string? FileName { get; set; }
        public FileStatus? FileStatus { get; set; }
        public string? MinimumDirectXVersion { get; set; }
        public string? MinimumSystemRam { get; set; }
    }
}
