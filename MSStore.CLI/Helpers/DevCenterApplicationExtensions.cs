// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Packaged.Models;

namespace MSStore.CLI.Helpers
{
    internal static class DevCenterApplicationExtensions
    {
        public static string? GetAnySubmissionId(this DevCenterApplication application)
        {
            if (application.Id == null)
            {
                return null;
            }

            if (application.PendingApplicationSubmission?.Id != null)
            {
                return application.PendingApplicationSubmission.Id;
            }
            else if (application.LastPublishedApplicationSubmission?.Id != null)
            {
                return application.LastPublishedApplicationSubmission.Id;
            }

            return null;
        }
    }
}
