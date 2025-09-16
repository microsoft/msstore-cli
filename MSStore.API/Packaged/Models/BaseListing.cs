// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class BaseListing
    {
        public List<string>? Keywords { get; set; }
        public string? Description { get; set; }
        public List<string>? Features { get; set; }
        public string? ReleaseNotes { get; set; }
        public List<Image>? Images { get; set; }
        public List<string>? RecommendedHardware { get; set; }
        public List<string>? MinimumHardware { get; set; }
        public string? Title { get; set; }
        public string? LicenseTerms { get; set; }
        public string? CopyrightAndTrademarkInfo { get; set; }
        public string? ShortDescription { get; set; }
        public string? ShortTitle { get; set; }
        public string? VoiceTitle { get; set; }
        public string? DevStudio { get; set; }





    }
}
