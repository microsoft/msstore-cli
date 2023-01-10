// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.CLI.Helpers
{
    internal static class VersionExtensions
    {
        public static string ToVersionString(this Version version, bool ignoreRevision = false)
        {
            if (version.Revision != -1 && !ignoreRevision)
            {
                return version.ToString(4);
            }

            var sufix = ignoreRevision ? string.Empty : ".0";

            if (version.Build != -1)
            {
                return version.ToString(3) + sufix;
            }

            return $"{version}.0" + sufix;
        }
    }
}
