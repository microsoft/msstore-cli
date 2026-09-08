// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services
{
    internal interface IEnvironmentInformationService
    {
        bool IsRunningOnCI { get; }

        /// <summary>
        /// Reads an environment variable. Going through this service rather than
        /// <see cref="System.Environment"/> directly keeps consumers testable without
        /// mutating process-wide state.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value, or null when the variable is not set.</returns>
        string? GetEnvironmentVariable(string name);
    }
}
