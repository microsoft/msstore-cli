// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.ProjectConfigurators
{
    internal class SubmissionImage
    {
        public SubmissionImage(string fileName, SubmissionImageType imageType)
        {
            FileName = fileName;
            ImageType = imageType;
        }

        public string FileName { get; private set; }
        public SubmissionImageType ImageType { get; private set; }
    }
}
