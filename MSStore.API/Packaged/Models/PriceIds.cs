// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;

namespace MSStore.API.Packaged.Models
{
    /// <summary>
    /// Well-known values for <see cref="Pricing.PriceId"/>, and the rules for which
    /// of them may be sent back to the Store submission API on an update.
    /// </summary>
    /// <remarks>
    /// These live outside <see cref="Pricing"/> on purpose. That type is serialized with
    /// <see cref="System.Text.Json.Serialization.JsonIgnoreCondition.Never"/>, so any member
    /// added to it would show up in the request body.
    /// </remarks>
    public static class PriceIds
    {
        /// <summary>
        /// Sentinel meaning "the price tier is not set; use the base price for the app".
        /// It is a legal value inside <see cref="Pricing.MarketSpecificPricings"/>, but the
        /// API also returns it as the <em>base</em> price of products managed by the newer
        /// per-market pricing model - where it cannot be sent back. Updating a submission
        /// with it fails with <c>'Base' is not a valid PriceId for base price.</c>
        /// </summary>
        public const string Base = "Base";

        /// <summary>The app is free.</summary>
        public const string Free = "Free";

        /// <summary>The app is not available in the given market.</summary>
        public const string NotAvailable = "NotAvailable";

        private const string TierPrefix = "Tier";

        /// <summary>
        /// Whether a price id read from a submission can be sent back unchanged on update.
        /// </summary>
        /// <remarks>
        /// Everything except <see cref="Base"/> (and a missing value) round-trips. Note that
        /// an empty price id must never be sent: the API answers <c>200 OK</c> and silently
        /// resets the product to free, which is how paid apps lost their price.
        /// </remarks>
        /// <param name="priceId">The price id to check.</param>
        /// <returns><c>true</c> when <paramref name="priceId"/> is safe to send back.</returns>
        public static bool IsRoundTrippable(string? priceId) =>
            !string.IsNullOrWhiteSpace(priceId) &&
            !string.Equals(priceId, Base, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Validates a user supplied price id and converts it to the casing the API expects.
        /// </summary>
        /// <remarks>
        /// Tier numbers are deliberately not range checked. The documented ranges
        /// (<c>Tier2</c>-<c>Tier96</c> and <c>Tier1012</c>-<c>Tier1424</c>) describe what a
        /// dashboard offers, not what the API accepts, and <c>isAdvancedPricingModel</c> is
        /// not a reliable way to tell the two apart - the API reports it inconsistently for
        /// the same product. Let the service reject an out of range tier.
        /// </remarks>
        /// <param name="priceId">The price id to normalize.</param>
        /// <param name="normalized">The normalized price id, when valid.</param>
        /// <returns><c>true</c> when <paramref name="priceId"/> is a value the API accepts.</returns>
        public static bool TryNormalize(string? priceId, out string? normalized)
        {
            normalized = null;

            if (string.IsNullOrWhiteSpace(priceId))
            {
                return false;
            }

            var trimmed = priceId.Trim();

            if (string.Equals(trimmed, Free, StringComparison.OrdinalIgnoreCase))
            {
                normalized = Free;
                return true;
            }

            if (string.Equals(trimmed, NotAvailable, StringComparison.OrdinalIgnoreCase))
            {
                normalized = NotAvailable;
                return true;
            }

            if (!trimmed.StartsWith(TierPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var tier = trimmed[TierPrefix.Length..];

            if (tier.Length == 0 || !int.TryParse(tier, NumberStyles.None, CultureInfo.InvariantCulture, out var tierNumber))
            {
                return false;
            }

            normalized = string.Concat(TierPrefix, tierNumber.ToString(CultureInfo.InvariantCulture));
            return true;
        }
    }
}
