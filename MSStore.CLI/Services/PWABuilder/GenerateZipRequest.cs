// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services.PWABuilder
{
    internal class GenerateZipRequest
    {
        public string? Url { get; set; }
        public string? Name { get; set; }
        public string? PackageId { get; set; }
        public string? Version { get; set; }
        public bool AllowSigning { get; set; }
        public ClassicPackage? ClassicPackage { get; set; }
        public Publisher? Publisher { get; set; }
        public string? ResourceLanguage { get; set; }
    }
}