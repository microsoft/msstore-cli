// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;

namespace MSStore.API.Packaged.Models
{
    public class DevCenterListing
    {
        public BaseListing? BaseListing { get; set; }
        public Dictionary<string, JsonElement>? PlatformOverrides { get; set; }
    }
}
