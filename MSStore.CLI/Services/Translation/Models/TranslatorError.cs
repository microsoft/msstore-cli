// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services.Translation.Models
{
    internal class TranslatorError
    {
        /// <summary>
        /// Gets or sets the six-digit error code, made of the three-digit HTTP status
        /// followed by a three-digit sub-code. The service returns this as a JSON number,
        /// not a string, despite what the published Swagger says.
        /// </summary>
        public int Code { get; set; }

        public string? Message { get; set; }
    }
}
