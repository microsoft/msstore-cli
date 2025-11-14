// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace MSStore.CLI.Services
{
    internal class EnvironmentInfo
    {
        // List of CI/CD and other environment variables to check
        private static readonly List<string> CIEnvironmentVariables = new List<string>
            {
                "GITHUB_ACTIONS", // Running inside GitHub Actions
                "TF_BUILD", // Running inside Azure DevOps
                "JENKINS_HOME", // Running inside Jenkins
                "GITLAB_CI", // Running inside GitLab CI
                "CIRCLECI", // Running inside CircleCI
                "TRAVIS", // Running inside Travis CI
                "CI", // Running inside a generic CI environment
                "CLI" // Running inside the CLI
            };

        // Cached environment information, loaded only once
        private static readonly Lazy<string> _cachedEnvironmentInfo = new Lazy<string>(ComputeEnvironmentInfo);

        public EnvironmentInfo()
        {
        }

        /// <summary>
        /// Gets the cached environment information. This method computes the environment data only once.
        /// </summary>
        /// <returns>A dictionary containing environment information with "Source" key indicating the detected CI/CD environment.</returns>
        public static string GetEnvironmentInfo()
        {
            // Return a copy of the cached data to prevent external modifications
            return _cachedEnvironmentInfo.Value;
        }

        /// <summary>
        /// Computes the environment information by detecting CI/CD environment variables.
        /// This method is called only once when the lazy value is first accessed.
        /// </summary>
        /// <returns>A dictionary containing the computed environment information.</returns>
        private static string ComputeEnvironmentInfo()
        {
            string environmentVar = "CLI"; // Default value

            // Iterate through CI/CD environment variables and add detected ones to properties
            foreach (string envVar in CIEnvironmentVariables)
            {
                string? envValue = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(envValue) &&
                    (envValue.Equals("true", StringComparison.OrdinalIgnoreCase) || envValue == "1"))
                {
                    environmentVar = envVar;
                    break; // Use the first detected environment variable
                }
            }
            return environmentVar;
        }
    }
}