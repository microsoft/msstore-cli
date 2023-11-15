// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.CLI.Helpers
{
    internal static class ListExtensions
    {
        public static bool IsNullOrEmpty<T>(this List<T>? list)
        {
            return list == null || list.Count == 0;
        }

        public static bool IsNullOrEmpty<TKey, TValue>(this Dictionary<TKey, TValue>? dict)
            where TKey : notnull
        {
            return dict == null || dict.Count == 0;
        }
    }
}
