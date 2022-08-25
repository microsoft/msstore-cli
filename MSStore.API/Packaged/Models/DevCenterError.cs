// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class DevCenterError
    {
        public string? Code { get; set; }
        public List<string>? Data { get; set; }
        public List<string>? Details { get; set; }
        public string? Message { get; set; }
        public string? Source { get; set; }
        public string? Target { get; set; }
    }
}
