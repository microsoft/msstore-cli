// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MSStore.API;

namespace MSStore.CLI.Services
{
    internal class AzureBlobManager() : IAzureBlobManager
    {
        public async Task<string> UploadFileAsync(string blobUri, string localFilePath, IProgress<double> progress, long uploadTimeout, CancellationToken ct)
        {
            using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

            // Capture the length up front. Progress<T> dispatches its handlers on the thread pool, so a
            // callback can still run after this method has returned (or thrown) and fileStream has been
            // disposed. The callback must therefore never touch fileStream.
            var totalBytes = fileStream.Length;

            var blobClientOptions = new BlobClientOptions();
            blobClientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(uploadTimeout);
            blobClientOptions.AddPolicy(new AddCorrelationIdHeaderPolicy(), HttpPipelinePosition.PerCall);
            var blobClient = new BlobClient(new Uri(blobUri.Replace("+", "%2B")), blobClientOptions);
            var blobUploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/zip"
                },
                ProgressHandler = new Progress<long>(CreateProgressCallback(totalBytes, progress)),
            };

            var response = await blobClient.UploadAsync(fileStream, blobUploadOptions, ct);
            if (response.Value != null)
            {
                return response.Value.ETag.ToString();
            }
            else
            {
                throw new MSStoreException(response.GetRawResponse().ReasonPhrase);
            }
        }

        internal static Action<long> CreateProgressCallback(long totalBytes, IProgress<double> progress)
        {
            return bytesTransferred =>
            {
                try
                {
                    if (totalBytes > 0)
                    {
                        progress.Report((double)bytesTransferred * 100 / totalBytes);
                    }
                }
                catch (Exception)
                {
                    // Progress reporting is best-effort. This runs on a thread-pool thread, outside the
                    // caller's try/catch, so anything that escapes here would terminate the process.
                }
            };
        }

        public class AddCorrelationIdHeaderPolicy() : HttpPipelineSynchronousPolicy
        {
            public override void OnSendingRequest(HttpMessage message)
            {
                message.Request.Headers.Add("ms-correlationid", Program.SessionId);
                base.OnSendingRequest(message);
            }
        }
    }
}
