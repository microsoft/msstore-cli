// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace MSStore.CLI.Services
{
    internal class ImageConverter(ILogger<ImageConverter> logger) : IImageConverter
    {
        private ILogger<ImageConverter> _logger = logger;

        public async Task<bool> ConvertIcoToPngAsync(string sourceFilePath, string destinationFilePath, int destinationWidth, int destinationHeight, int paddingX, int paddingY, CancellationToken ct)
        {
            try
            {
                using var bitmap = SKBitmap.Decode(sourceFilePath);

                SKBitmap finalBitmap = new SKBitmap(destinationWidth, destinationHeight);
                SKRect dest = new SKRect(paddingX, paddingY, destinationWidth - paddingX, destinationHeight - paddingY);
                SKRect source = new SKRect(0, 0, bitmap.Width, bitmap.Height);

                using (SKCanvas canvas = new SKCanvas(finalBitmap))
                {
                    canvas.DrawBitmap(bitmap, source, dest, new SKPaint
                    {
                        FilterQuality = SKFilterQuality.High
                    });
                }

                using MemoryStream memStream = new MemoryStream();
                using (SKManagedWStream wstream = new SKManagedWStream(memStream))
                {
                    finalBitmap.Encode(wstream, SKEncodedImageFormat.Png, 100);
                }

                memStream.Seek(0, SeekOrigin.Begin);

                using var fileStream = new FileStream(destinationFilePath, FileMode.Create);
                await memStream.CopyToAsync(fileStream, ct);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to convert ICO to PNG");
                return false;
            }
        }

        public byte[]? ConvertToByteArray(string image)
        {
            using var bitmap = SKBitmap.Decode(image);
            return bitmap?.Bytes;
        }
    }
}
