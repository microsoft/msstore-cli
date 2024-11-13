// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.ProjectConfigurators
{
    internal class SubmissionImage(string fileName, SubmissionImageType imageType)
    {
        public string FileName { get; private set; } = fileName;
        public SubmissionImageType ImageType { get; private set; } = imageType;
    }
}
