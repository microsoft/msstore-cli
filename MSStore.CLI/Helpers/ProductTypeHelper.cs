// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.CLI.Helpers
{
    internal static class ProductTypeHelper
    {
        public static ProductType Solve(string productId)
        {
            if (Guid.TryParse(productId, out _))
            {
                return ProductType.Unpackaged;
            }
            else
            {
                return ProductType.Packaged;
            }
        }
    }
}
