// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Help;

namespace MSStore.CLI
{
    internal static class CommandExtensions
    {
        public static void SetDefaultHelpHandler(this Command command)
        {
            command.SetHandler((context) =>
            {
                HelpBuilder helpBuilder = new(LocalizationResources.Instance, GetBufferWidth());
                helpBuilder.Write(command, Console.Out);
            });
        }

        internal static int GetBufferWidth()
        {
            try
            {
                return Console.BufferWidth;
            }
            catch
            {
                // Default to 240
                return 240;
            }
        }
    }
}
