// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal static class ProjectImagesHelper
    {
        internal static List<string> GetDefaultImagesUsedByApp(List<string> appImagesFileList, List<string>? projectSpecificDefaultImagesFileList, IImageConverter imageConverter, ILogger logger)
        {
            List<string> defaultImagesFileList = GetDefaultImagesList();
            if (projectSpecificDefaultImagesFileList != null)
            {
                defaultImagesFileList.AddRange(projectSpecificDefaultImagesFileList);
            }

            List<string> failedImages = new List<string>();
            List<byte[]> defaultImages = GetHashesForImageFiles(defaultImagesFileList, imageConverter);
            List<byte[]> appImages = GetHashesForImageFiles(appImagesFileList, imageConverter);

            for (int appImageIndex = 0; appImageIndex < appImages.Count; appImageIndex++)
            {
                for (int defaultImageIndex = 0; defaultImageIndex < defaultImages.Count; defaultImageIndex++)
                {
                    if (appImages[appImageIndex].SequenceEqual(defaultImages[defaultImageIndex]))
                    {
                        logger.LogInformation("File {ImageFile} matches against {DefaultImage}", appImagesFileList[appImageIndex], defaultImagesFileList[defaultImageIndex]);
                        failedImages.Add(appImagesFileList[appImageIndex]);
                        break;
                    }
                }
            }

            return failedImages;
        }

        private static List<string> GetDefaultImagesList()
        {
            var images = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                images.AddRange(GetWindowsSdkDefaultImages());
            }

            return images;
        }

        [SupportedOSPlatform("windows")]
        private static string[] GetWindowsSdkDefaultImages()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string defaultImagesDir = Path.Combine(programFiles, "Windows Kits", "10", "App Certification Kit", "DefaultAppImages");

            return Directory.GetFiles(defaultImagesDir, "*.*", SearchOption.AllDirectories);
        }

        private static List<byte[]> GetHashesForImageFiles(List<string> imageFiles, IImageConverter imageConverter)
        {
            List<byte[]> hashes = new List<byte[]>();

            foreach (string image in imageFiles)
            {
                try
                {
                    var byteArray = imageConverter.ConvertToByteArray(image);
                    if (byteArray == null)
                    {
                        AnsiConsole.WriteLine($"Cannot load the image: {image}.");
                        continue;
                    }

                    hashes.Add(SHA512.HashData(byteArray));
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteLine($"Cannot load the image: {image}.");
                    AnsiConsole.WriteLine(ex.ToString());
                    continue;
                }
            }

            return hashes;
        }
    }
}
