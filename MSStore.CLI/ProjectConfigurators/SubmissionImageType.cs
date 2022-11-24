// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.ProjectConfigurators
{
    internal enum SubmissionImageType
    {
        // Screenshot images
        Screenshot,
        MobileScreenshot,
        XboxScreenshot,
        SurfaceHubScreenshot,
        HoloLensScreenshot,

        // Store logos
        StoreLogo9x16,
        StoreLogoSquare,
        Icon,

        // Promotional images
        PromotionalArt16x9,
        PromotionalArtwork2400X1200,

        // Xbox images
        XboxBrandedKeyArt,
        XboxTitledHeroArt,
        XboxFeaturedPromotionalArt,

        // Optional promotional images:
        SquareIcon358X358,
        BackgroundImage1000X800,
        PromotionalArtwork414X180,
    }
}
