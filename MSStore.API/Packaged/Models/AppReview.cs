// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace MSStore.API.Packaged.Models
{
    /// <summary>
    /// A single customer review of an application, as returned by the Microsoft Store
    /// analytics API (<c>/v1.0/my/analytics/reviews</c>).
    /// </summary>
    /// <remarks>
    /// Every property is optional. The service omits fields entirely (rather than
    /// returning them as null) whenever the data was not captured for a given review.
    /// </remarks>
    public class AppReview
    {
        /// <summary>
        /// Gets or sets the unique identifier of the review.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the date the review was submitted. The service returns this as a
        /// string in US format (for example <c>3/5/2021 12:48:33 PM</c>) rather than ISO-8601,
        /// so it is kept as a string to round-trip exactly.
        /// </summary>
        public string? Date { get; set; }

        public string? ApplicationId { get; set; }
        public string? ApplicationName { get; set; }

        /// <summary>
        /// Gets or sets the ISO 3166 country code of the market the review was submitted in.
        /// This is a country, not a language.
        /// </summary>
        public string? Market { get; set; }

        public string? OsVersion { get; set; }
        public string? DeviceType { get; set; }
        public bool? IsRevised { get; set; }
        public string? PackageVersion { get; set; }
        public string? DeviceModel { get; set; }
        public string? ProductFamily { get; set; }
        public long? DeviceRAM { get; set; }
        public string? DeviceScreenResolution { get; set; }
        public double? DeviceStorageCapacity { get; set; }
        public bool? IsTouchEnabled { get; set; }
        public string? ReviewerName { get; set; }
        public double? Rating { get; set; }
        public string? ReviewTitle { get; set; }
        public string? ReviewText { get; set; }
        public long? HelpfulCount { get; set; }
        public long? NotHelpfulCount { get; set; }
        public string? ResponseDate { get; set; }
        public string? ResponseText { get; set; }

        /// <summary>
        /// Gets or sets the review title translated into the requested language. This is
        /// never returned by the Store, which offers no translation; it is populated by the
        /// CLI when translation is requested, and omitted from output otherwise.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TranslatedReviewTitle { get; set; }

        /// <summary>
        /// Gets or sets the review text translated into the requested language. This is
        /// never returned by the Store, which offers no translation; it is populated by the
        /// CLI when translation is requested, and omitted from output otherwise.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TranslatedReviewText { get; set; }

        /// <summary>
        /// Gets or sets the language detected in the original review text. The Store returns
        /// no language information, so this comes from the translation service and is only
        /// present when translation was requested.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DetectedLanguage { get; set; }
    }
}
