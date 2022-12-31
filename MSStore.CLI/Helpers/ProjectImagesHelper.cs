// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal static class ProjectImagesHelper
    {
        internal static async Task<List<string>> GetDefaultImagesUsedByAppAsync(List<string> appImagesFileList, IImageConverter imageConverter, IExternalCommandExecutor externalCommandExecutor, ILogger logger, CancellationToken ct)
        {
            List<string> defaultImagesFileList = await GetDefaultImagesListAsync(externalCommandExecutor, ct);
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

        private static async Task<List<string>> GetDefaultImagesListAsync(IExternalCommandExecutor externalCommandExecutor, CancellationToken ct)
        {
            var images = new List<string>();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string defaultImagesDir = Path.Combine(programFiles, "Windows Kits", "10", "App Certification Kit", "DefaultAppImages");

                images.AddRange(Directory.GetFiles(defaultImagesDir, "*.*", SearchOption.AllDirectories));
            }

            // Add Defaut Flutter images
            var flutterExecutablePath = await externalCommandExecutor.FindToolAsync("flutter", ct);
            if (!string.IsNullOrEmpty(flutterExecutablePath))
            {
                var bin = Directory.GetParent(flutterExecutablePath);
                if (bin != null)
                {
                    var flutterDir = bin.Parent;
                    if (flutterDir != null && flutterDir.FullName != null)
                    {
                        var iconSubPath = Path.Combine("templates", "app_shared", "windows.tmpl", "runner", "resources", "app_icon.ico");
                        var iconTemplateSubPath = $"{iconSubPath}.img.tmpl";
                        var flutterDefaultAppIconTemplate = Path.Combine(flutterDir.FullName, "packages", "flutter_tools", iconTemplateSubPath);
                        if (File.Exists(flutterDefaultAppIconTemplate))
                        {
                            var pubDartlangOrgDir = new DirectoryInfo(Path.Combine(flutterDir.FullName, ".pub-cache", "hosted", "pub.dartlang.org"));
                            if (pubDartlangOrgDir.Exists)
                            {
                                var flutter_template_images = pubDartlangOrgDir
                                    .GetDirectories("flutter_template_images-*", SearchOption.TopDirectoryOnly)
                                    .OrderByDescending(d => d.Name)
                                    .FirstOrDefault();
                                if (flutter_template_images?.FullName != null)
                                {
                                    var flutterDefaultAppIcon = Path.Combine(flutter_template_images.FullName, iconSubPath);
                                    if (File.Exists(flutterDefaultAppIcon))
                                    {
                                        images.Add(flutterDefaultAppIcon);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return images;
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
