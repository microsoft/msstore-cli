// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MSStore.API;
using MSStore.API.Packaged;

namespace MSStore.CLI.Services
{
    internal interface IStoreAPIFactory
    {
        Task<IStoreAPI> CreateAsync(Configurations? config = null, CancellationToken ct = default);

        /// <summary>
        /// Creates an <see cref="IStoreAPI"/> for a candidate configuration, using the supplied secret instead of
        /// reading one from the credential store. This allows a configuration to be validated without first having
        /// to stage (or erase) credentials in the OS credential store.
        /// </summary>
        /// <param name="config">The candidate configuration.</param>
        /// <param name="secret">
        /// The client secret, or the certificate file password. A <see langword="null"/> value explicitly means
        /// "this configuration has no secret" (certificate thumbprint, client assertion, or password-less
        /// certificate file), and never falls back to the credential store.
        /// </param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The <see cref="IStoreAPI"/> instance.</returns>
        Task<IStoreAPI> CreateWithSecretAsync(Configurations config, string? secret, CancellationToken ct = default);

        Task<IStorePackagedAPI> CreatePackagedAsync(Configurations? config = null, CancellationToken ct = default);
    }
}
