// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Numerics;

namespace MSStore.CLI.Services.PWABuilder
{
    internal static class IconExtensions
    {
        public static Vector2 GetSize(this Icon icon)
        {
            var sizes = icon.Sizes;
            if(string.IsNullOrEmpty(sizes))
            {
                return default;
            }

            var dimensions = sizes.Split('x');
            return new Vector2(int.Parse(dimensions[0], CultureInfo.InvariantCulture), int.Parse(dimensions[1], CultureInfo.InvariantCulture));
        }
    }
}
