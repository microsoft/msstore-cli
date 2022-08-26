// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.CLI.Services.PWABuilder
{
    internal class WebManifestJson
    {
        public string? Description { get; set; }
        public string? Display { get; set; }
        public List<Icon>? Icons { get; set; }
        public string? Name { get; set; }
        public List<ScreenShot>? Screenshots { get; set; }
    }
}
