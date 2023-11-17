// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Text.Json;

namespace MSStore.API.Packaged.Models
{
    public class UpperCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            // Forces 1st letter of enum to be capitalized (required by Store REST API)
            return name.Length >= 1 ? string.Concat(name[0].ToString().ToUpper(CultureInfo.InvariantCulture), name.AsSpan(1)) : name;
        }
    }
}
