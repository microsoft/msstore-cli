// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using MSStore.API;

namespace MSStore.CLI.Commands.Reviews
{
    internal static class ReviewsFailure
    {
        /// <summary>
        /// Gets the HTTP status of a failed Store request, however it was raised.
        /// </summary>
        /// <remarks>
        /// Errors with a body arrive as a plain <see cref="MSStoreException"/> carrying the status,
        /// and errors without one as an <see cref="MSStoreHttpException"/> carrying the response.
        /// </remarks>
        /// <param name="error">The failure.</param>
        /// <returns>The status, or null when the failure did not come from an HTTP response.</returns>
        public static HttpStatusCode? GetStatusCode(MSStoreException error) =>
            error is MSStoreHttpException http ? http.Response.StatusCode : error.StatusCode;

        /// <summary>
        /// Describes a failed reviews request for the user.
        /// </summary>
        /// <remarks>
        /// The analytics API answers a product ID that does not exist and one that belongs to
        /// another account the same way, with an authorization failure, so the message covers
        /// both rather than guessing which one it was.
        /// </remarks>
        /// <param name="error">The failure.</param>
        /// <returns>The message to show.</returns>
        public static string Describe(MSStoreException error) =>
            GetStatusCode(error) is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? "Could not read the reviews for this product. Check that the product ID is correct and that it belongs to this account."
                : "Error while retrieving Reviews.";
    }
}
