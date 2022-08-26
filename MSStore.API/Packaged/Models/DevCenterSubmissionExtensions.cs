// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace MSStore.API.Packaged.Models
{
    public static class DevCenterSubmissionExtensions
    {
        public static List<ApplicationPackage> FilterUnsupported(this List<ApplicationPackage>? applicationPackages)
        {
            if (applicationPackages == null)
            {
                return new List<ApplicationPackage>();
            }

            return applicationPackages.Where(p =>
                    p.Capabilities == null ||
                        (!p.Capabilities.Contains("Microsoft.storeFilter.core.notSupported_8wekyb3d8bbwe")
                        && !p.Capabilities.Contains("coreNotSupported")))
                .ToList();
        }
    }
}
