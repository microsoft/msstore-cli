// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API;
using MSStore.API.Models;

namespace MSStore.CLI.Helpers
{
    internal static class IStoreAPIExtensions
    {
        public static async IAsyncEnumerable<ResponseWrapper<SubmissionStatus>> PollSubmissionStatusAsync(this IStoreAPI storeAPI, string productId, string submissionId, bool waitFirst, [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Let's periodically check the status until it changes from "CommitsStarted" to either
            // successful status or a failure.
            ResponseWrapper<SubmissionStatus>? submissionStatus;
            do
            {
                if (waitFirst)
                {
                    await Task.Delay(API.Packaged.StorePackagedAPI.DefaultSubmissionPollDelay, ct);
                }

                waitFirst = true;
                submissionStatus = await storeAPI.GetSubmissionStatusPollingAsync(productId, submissionId, ct);

                if (!submissionStatus.IsSuccess || submissionStatus.ResponseData == null || submissionStatus.ResponseData.HasFailed)
                {
                    /*
                    var errorResponse = submissionStatus as ErrorResponse;
                    if (errorResponse.StatusCode == 401)
                    {
                        Debug.WriteLine($"Access token expired. Requesting new one. (message='{errorResponse.Message}')");
                        await InitAsync(ct);
                        status = new SubmissionStatus
                        {
                            HasFailed = false
                        };
                        continue;
                    }
                    */

                    Debug.WriteLine("Error");
                    throw new NotImplementedException("Error");
                }

                yield return submissionStatus;
            }
            while (!submissionStatus.ResponseData.HasFailed
                    && submissionStatus.ResponseData.PublishingStatus is not PublishingStatus.PUBLISHED
                                                                     and not PublishingStatus.FAILED);
        }
    }
}
