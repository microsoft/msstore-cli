// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;

internal class Program
{
    public static void Main(params string[] args)
    {
        try
        {
            var path = args[0].TrimEnd('"');

            DirectoryInfo dirInfo;
            try
            {
                dirInfo = new DirectoryInfo(path);
            }
            catch
            {
                return;
            }

            if (!dirInfo.Exists)
            {
                return;
            }

            var value = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);

            if (value?.ToUpper(CultureInfo.InvariantCulture).Contains(dirInfo.ToString().ToUpper(CultureInfo.InvariantCulture)) == false)
            {
                value += ";" + dirInfo.ToString();
                Environment.SetEnvironmentVariable("Path", value, EnvironmentVariableTarget.Machine);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
}