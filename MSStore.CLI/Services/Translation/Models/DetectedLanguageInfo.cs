// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services.Translation.Models
{
    internal class DetectedLanguageInfo
    {
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the confidence of the detection, between 0.0 and 1.0. The published
        /// Swagger types this as an integer, which is wrong; the service returns a float.
        /// </summary>
        public double Score { get; set; }
    }
}
