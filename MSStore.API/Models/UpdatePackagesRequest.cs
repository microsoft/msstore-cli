// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class UpdatePackagesRequest
    {
        public List<Package>? Packages { get; set; }
    }
}
