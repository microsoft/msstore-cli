// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class Package
    {
        public string? PackageUrl { get; set; }
        public List<string>? Languages { get; set; }
        public List<string>? Architectures { get; set; }
        public string? InstallerParameters { get; set; }
        public bool IsSilentInstall { get; set; }
        public string? GenericDocUrl { get; set; }
        public List<ErrorDetail>? ErrorDetails { get; set; }
        public string? PackageType { get; set; }
        public string? PackageId { get; set; }
    }
}
