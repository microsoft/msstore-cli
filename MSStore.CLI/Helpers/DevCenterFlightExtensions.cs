// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Packaged.Models;

namespace MSStore.CLI.Helpers
{
    internal static class DevCenterFlightExtensions
    {
        public static string? GetAnyFlightSubmissionId(this DevCenterFlight flight)
        {
            if (flight.FlightId == null)
            {
                return null;
            }

            if (flight.PendingFlightSubmission?.Id != null)
            {
                return flight.PendingFlightSubmission.Id;
            }
            else if (flight.LastPublishedFlightSubmission?.Id != null)
            {
                return flight.LastPublishedFlightSubmission.Id;
            }

            return null;
        }
    }
}
