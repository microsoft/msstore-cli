// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.Graph
{
    internal class ProgressableStreamContent : HttpContent
    {
        private const int DefaultBufferSize = 5 * 4096;

        private IProgress<double> _progress;
        private HttpContent _content;
        private int _bufferSize;

        public ProgressableStreamContent(HttpContent content, IProgress<double> progress)
            : this(content, DefaultBufferSize, progress)
        {
        }

        public ProgressableStreamContent(HttpContent content, int bufferSize, IProgress<double> progress)
        {
            _content = content;
            _bufferSize = bufferSize;
            _progress = progress;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            return InternalSerializeToStreamAsync(stream, cancellationToken);
        }

        private Task InternalSerializeToStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            return Task.Run(
                async () =>
                {
                    var buffer = new byte[_bufferSize];
                    long size;
                    TryComputeLength(out size);
                    var uploaded = 0;

                    using (var sinput = await _content.ReadAsStreamAsync(cancellationToken))
                    {
                        while (true)
                        {
                            var length = await sinput.ReadAsync(buffer, cancellationToken);
                            if (length <= 0)
                            {
                                break;
                            }

                            uploaded += length;
                            _progress.Report(uploaded * 100.0 / size);

                            await stream.WriteAsync(buffer.AsMemory(0, length), cancellationToken);
                            await stream.FlushAsync(cancellationToken);
                        }
                    }

                    _progress.Report(100);

                    stream.Flush();
                }, cancellationToken);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return InternalSerializeToStreamAsync(stream, CancellationToken.None);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Headers.ContentLength.GetValueOrDefault();
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _content.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
