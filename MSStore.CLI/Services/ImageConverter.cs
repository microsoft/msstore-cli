// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace MSStore.CLI.Services
{
    internal class ImageConverter : IImageConverter
    {
        public async Task<bool> ConvertIcoToPngAsync(string sourceFilePath, string destinationFilePath, CancellationToken ct)
        {
            try
            {
                using var bitmap = SKBitmap.Decode(sourceFilePath);

                using MemoryStream memStream = new MemoryStream();
                using (SKManagedWStream wstream = new SKManagedWStream(memStream))
                {
                    bitmap.Encode(wstream, SKEncodedImageFormat.Png, 100);
                }

                memStream.Seek(0, SeekOrigin.Begin);

                using var fileStream = new FileStream(destinationFilePath, FileMode.Create);
                await memStream.CopyToAsync(fileStream, ct);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
