// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class Trailer
    {
        public string? Id { get; set; }
        public string? VideoFileName { get; set; }
        public string? VideoFileId { get; set; }
        public Dictionary<string, TrailerAsset>? TrailerAssets { get; set; }
    }
}
