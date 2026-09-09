// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Helpers
{
    /// <summary>
    /// The standard stream that human-readable console output is written to.
    /// </summary>
    /// <remarks>
    /// This never affects machine-readable payloads, which always go to stdout through
    /// <see cref="StandardOutput"/>.
    /// </remarks>
    internal enum OutputStream
    {
        /// <summary>
        /// Human-readable output goes to standard error. This is the default, and keeps stdout
        /// clean so payloads can be piped or captured.
        /// </summary>
        Stderr,

        /// <summary>
        /// Human-readable output goes to standard output. Useful on Azure DevOps, which renders
        /// every stderr line as <c>##[error]</c>.
        /// </summary>
        Stdout
    }
}
