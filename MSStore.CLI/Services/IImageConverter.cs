// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal interface IImageConverter
    {
        Task<bool> ConvertIcoToPngAsync(string sourceFilePath, string destinationFilePath, int destinationWidth, int destinationHeight, int paddingX, int paddingY, CancellationToken ct);
        byte[]? ConvertToByteArray(string image);
    }
}
