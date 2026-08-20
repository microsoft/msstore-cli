// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class AzureBlobManagerUnitTests
    {
        private sealed class RecordingProgress : IProgress<double>
        {
            public List<double> Reported { get; } = [];

            public void Report(double value) => Reported.Add(value);
        }

        private sealed class ThrowingProgress : IProgress<double>
        {
            public void Report(double value) => throw new InvalidOperationException("Progress display already torn down.");
        }

        /// <summary>
        /// Regression test for https://github.com/microsoft/msstore-cli/issues/154.
        ///
        /// The upload progress callback used to read fileStream.Length. Progress&lt;T&gt; dispatches its
        /// handlers on the thread pool, so a queued callback could run after UploadFileAsync had returned or
        /// thrown and the `using` had disposed the stream. That threw ObjectDisposedException on a
        /// thread-pool thread, outside the caller's try/catch, and took the whole process down.
        /// </summary>
        [TestMethod]
        public void CreateProgressCallbackDoesNotTouchTheFileStreamAfterItIsDisposed()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllBytes(path, new byte[1000]);

                var progress = new RecordingProgress();
                Action<long> callback;

                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    callback = AzureBlobManager.CreateProgressCallback(fileStream.Length, progress);
                }

                // The stream is now disposed, exactly as it is when a late callback is dispatched.
                callback(250);

                Assert.AreSequenceEqual([25d], progress.Reported);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void CreateProgressCallbackReportsPercentage()
        {
            var progress = new RecordingProgress();
            var callback = AzureBlobManager.CreateProgressCallback(200, progress);

            callback(0);
            callback(50);
            callback(200);

            Assert.AreSequenceEqual([0d, 25d, 100d], progress.Reported);
        }

        [TestMethod]
        public void CreateProgressCallbackDoesNotReportForAnEmptyFile()
        {
            var progress = new RecordingProgress();
            var callback = AzureBlobManager.CreateProgressCallback(0, progress);

            // Must not divide by zero and push NaN/Infinity into the progress display.
            callback(0);

            Assert.IsEmpty(progress.Reported);
        }

        [TestMethod]
        public void CreateProgressCallbackSwallowsExceptionsFromTheProgressConsumer()
        {
            // The CLI passes a Spectre.Console ProgressTask, which can throw once the progress display has
            // been torn down. A late callback runs on a thread-pool thread, so anything escaping here would
            // terminate the process.
            var callback = AzureBlobManager.CreateProgressCallback(100, new ThrowingProgress());

            callback(50);
        }
    }
}
