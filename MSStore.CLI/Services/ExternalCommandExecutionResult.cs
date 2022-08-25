// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services
{
    internal struct ExternalCommandExecutionResult
    {
        public int ExitCode;
        public string StdOut;
        public string StdErr;
    }
}
