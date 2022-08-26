// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class TrailerAsset
    {
        public string? Title { get; set; }
        public List<ImageResource>? ImageList { get; set; }
    }
}
