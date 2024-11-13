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
using Microsoft.ApplicationInsights;
using MSStore.API;

namespace MSStore.CLI.Services
{
    internal class AzureBlobManager(TelemetryClient telemetryClient) : IAzureBlobManager
    {
        private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

        public async Task<string> UploadFileAsync(string blobUri, string localFilePath, IProgress<double> progress, CancellationToken ct)
        {
            using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

            var blobClientOptions = new BlobClientOptions();
            blobClientOptions.AddPolicy(new AddCorrelationIdHeaderPolicy(_telemetryClient), HttpPipelinePosition.PerCall);
            var blobClient = new BlobClient(new Uri(blobUri.Replace("+", "%2B")), blobClientOptions);
            var blobUploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/zip"
                },
                ProgressHandler = new Progress<long>(bytesTransferred =>
                {
                    progress.Report((double)bytesTransferred * 100 / fileStream.Length);
                }),
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

        public class AddCorrelationIdHeaderPolicy(TelemetryClient telemetryClient) : HttpPipelineSynchronousPolicy
        {
            public override void OnSendingRequest(HttpMessage message)
            {
                message.Request.Headers.Add("ms-correlationid", telemetryClient.Context.Session.Id);
                base.OnSendingRequest(message);
            }
        }
    }
}
