// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Packaged.Models;

namespace MSStore.CLI.Helpers
{
    internal static class DevCenterErrorExtensions
    {
        public static string ToErrorMessage(this DevCenterError error)
        {
            var message = $"Error Code: {error.Code}{System.Environment.NewLine}";
            message += $"Error Message: {error.Message}{System.Environment.NewLine}";
            message += $"Error Source: {error.Source}{System.Environment.NewLine}";
            message += $"Error Target: {error.Target}{System.Environment.NewLine}";

            message += $"Error Data:{System.Environment.NewLine}";
            if (error.Data?.Count > 0)
            {
                foreach (var data in error.Data)
                {
                    message += $"  {data}{System.Environment.NewLine}";
                }
            }
            else
            {
                message += $"  No data{System.Environment.NewLine}";
            }

            message += $"Error Details:{System.Environment.NewLine}";

            if (error.Details?.Count > 0)
            {
                foreach (var detail in error.Details)
                {
                    message += $"  {detail}{System.Environment.NewLine}";
                }
            }
            else
            {
                message += $"  No details{System.Environment.NewLine}";
            }

            return message;
        }
    }
}
