// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using MSStore.API;

namespace MSStore.CLI.Services.CredentialManager.Unix
{
    internal class CredentialManagerUnix : ICredentialManager
    {
        private static readonly string SchemaName = "com.microsoft.store.credentials";
        private static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("Service", "msstore-cli");
        private static readonly string LinuxKeyRingAttr2Key = "Account";
        private static readonly string LinuxKeyRingCollection = "default";

        [SupportedOSPlatform("macos")]
        private static string? ReadCredentialOSX(string userName)
        {
            IntPtr passwordDataPtr = IntPtr.Zero;
            IntPtr itemPtr = IntPtr.Zero;

            try
            {
                int resultStatus = NativeMethods.SecKeychainFindGenericPassword(
                    keychainOrArray: IntPtr.Zero,
                    serviceNameLength: (uint)SchemaName.Length,
                    serviceName: SchemaName,
                    accountNameLength: (uint)userName.Length,
                    accountName: userName,
                    passwordLength: out uint passwordLength,
                    passwordData: out passwordDataPtr,
                    itemRef: out itemPtr);

                byte[] content = Array.Empty<byte>();
                switch (resultStatus)
                {
                    case NativeMethods.SecResultCodes.ErrSecItemNotFound:
                        break;
                    case NativeMethods.SecResultCodes.ErrSecSuccess:
                        if (passwordLength > 0)
                        {
                            content = new byte[passwordLength];
                            Marshal.Copy(
                                source: passwordDataPtr,
                                destination: content,
                                startIndex: 0,
                                length: content.Length);
                        }

                        break;
                    default:
                        throw new MSStoreException($"Failed to read credential: {resultStatus}");
                }

                return Encoding.Default.GetString(content);
            }
            finally
            {
                FreePointers(ref itemPtr, ref passwordDataPtr);
            }
        }

        [SupportedOSPlatform("macos")]
        private static void WriteCredentialOSX(string userName, string secret)
        {
            IntPtr passwordDataPtr = IntPtr.Zero;
            IntPtr itemPtr = IntPtr.Zero;

            var passwordData = Encoding.Default.GetBytes(secret);

            try
            {
                int resultStatus = NativeMethods.SecKeychainFindGenericPassword(
                    keychainOrArray: IntPtr.Zero,
                    serviceNameLength: (uint)SchemaName.Length,
                    serviceName: SchemaName,
                    accountNameLength: (uint)userName.Length,
                    accountName: userName,
                    passwordLength: out uint passwordLength,
                    passwordData: out passwordDataPtr,
                    itemRef: out itemPtr);

                switch (resultStatus)
                {
                    case NativeMethods.SecResultCodes.ErrSecSuccess:
                        resultStatus = NativeMethods.SecKeychainItemModifyAttributesAndData(
                            itemRef: itemPtr,
                            attrList: IntPtr.Zero,
                            passwordLength: (uint)passwordData.Length,
                            passwordData: passwordData);

                        if (resultStatus != NativeMethods.SecResultCodes.ErrSecSuccess)
                        {
                            throw new MSStoreException($"Failed to write(update) credential: {resultStatus}");
                        }

                        break;

                    case NativeMethods.SecResultCodes.ErrSecItemNotFound:
                        resultStatus = NativeMethods.SecKeychainAddGenericPassword(
                            keychain: IntPtr.Zero,
                            serviceNameLength: (uint)SchemaName.Length,
                            serviceName: SchemaName,
                            accountNameLength: (uint)userName.Length,
                            accountName: userName,
                            passwordLength: (uint)passwordData.Length,
                            passwordData: passwordData,
                            itemRef: out itemPtr);

                        if (resultStatus != NativeMethods.SecResultCodes.ErrSecSuccess)
                        {
                            throw new MSStoreException($"Failed to write(new) credential: {resultStatus}");
                        }

                        break;
                    default:
                        throw new MSStoreException($"Failed to write credential: {resultStatus}");
                }
            }
            finally
            {
                FreePointers(ref itemPtr, ref passwordDataPtr);
            }
        }

        [SupportedOSPlatform("macos")]
        private static void ClearCredentialsOSX(string userName)
        {
            IntPtr passwordDataPtr = IntPtr.Zero;
            IntPtr itemPtr = IntPtr.Zero;

            try
            {
                int resultStatus = NativeMethods.SecKeychainFindGenericPassword(
                    keychainOrArray: IntPtr.Zero,
                    serviceNameLength: (uint)SchemaName.Length,
                    serviceName: SchemaName,
                    accountNameLength: (uint)userName.Length,
                    accountName: userName,
                    passwordLength: out uint passwordLength,
                    passwordData: out passwordDataPtr,
                    itemRef: out itemPtr);

                if (resultStatus == NativeMethods.SecResultCodes.ErrSecItemNotFound
                    || itemPtr == IntPtr.Zero)
                {
                    return;
                }

                _ = NativeMethods.SecKeychainItemDelete(itemPtr);
            }
            finally
            {
                FreePointers(ref itemPtr, ref passwordDataPtr);
            }
        }

        [SupportedOSPlatform("macos")]
        private static void FreePointers(ref IntPtr itemPtr, ref IntPtr passwordDataPtr)
        {
            if (itemPtr != IntPtr.Zero)
            {
                NativeMethods.CFRelease(itemPtr);
                itemPtr = IntPtr.Zero;
            }

            if (passwordDataPtr != IntPtr.Zero)
            {
                _ = NativeMethods.SecKeychainItemFreeContent(attrList: IntPtr.Zero, data: passwordDataPtr);
                passwordDataPtr = IntPtr.Zero;
            }
        }

        [SupportedOSPlatform("linux")]
        private Lazy<IntPtr> _libsecretSchema = new Lazy<IntPtr>(() =>
        {
            var libsecretSchema = NativeMethods.secret_schema_new(
                name: SchemaName,
                flags: (int)NativeMethods.SecretSchemaFlags.SECRET_SCHEMA_DONT_MATCH_NAME,
                attribute1: LinuxKeyRingAttr1.Key,
                attribute1Type: (int)NativeMethods.SecretSchemaAttributeType.SECRET_SCHEMA_ATTRIBUTE_STRING,
                attribute2: LinuxKeyRingAttr2Key,
                attribute2Type: (int)NativeMethods.SecretSchemaAttributeType.SECRET_SCHEMA_ATTRIBUTE_STRING,
                end: IntPtr.Zero);

            if (libsecretSchema == IntPtr.Zero)
            {
                throw new MSStoreException($"Failed to create libsecret schema!");
            }

            return libsecretSchema;
        });

        public string? ReadCredential(string userName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return ReadCredentialLinux(userName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return ReadCredentialOSX(userName);
            }
            else
            {
                throw new PlatformNotSupportedException($"{nameof(CredentialManagerUnix)} supports only Linux and OSX.");
            }
        }

        [SupportedOSPlatform("linux")]
        private string? ReadCredentialLinux(string userName)
        {
            IntPtr error;
            string secret = NativeMethods.secret_password_lookup_sync(
                schema: _libsecretSchema.Value,
                cancellable: IntPtr.Zero,
                error: out error,
                attribute1Type: LinuxKeyRingAttr1.Key,
                attribute1Value: LinuxKeyRingAttr1.Value,
                attribute2Type: LinuxKeyRingAttr2Key,
                attribute2Value: userName,
                end: IntPtr.Zero);

            if (error != IntPtr.Zero)
            {
                try
                {
                    if (Marshal.PtrToStructure(error, typeof(NativeMethods.GError)) is NativeMethods.GError err)
                    {
                        throw new MSStoreException($"Failed to read credential: Domain: '{err.Domain}' - Code: '{err.Code}' - Message: '{err.Message}'");
                    }
                }
                catch (Exception e)
                {
                    throw new MSStoreException("Failed to read credential", e);
                }
            }

            if (string.IsNullOrEmpty(secret))
            {
                return string.Empty;
            }
            else
            {
                return secret;
            }
        }

        public void WriteCredential(string userName, string secret)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                WriteCredentialLinux(userName, secret);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                WriteCredentialOSX(userName, secret);
            }
            else
            {
                throw new PlatformNotSupportedException($"{nameof(CredentialManagerUnix)} supports only Linux and OSX.");
            }
        }

        [SupportedOSPlatform("linux")]
        private void WriteCredentialLinux(string userName, string secret)
        {
            IntPtr error;
            _ = NativeMethods.secret_password_store_sync(
                schema: _libsecretSchema.Value,
                collection: LinuxKeyRingCollection,
                label: $"{userName}",
                password: secret,
                cancellable: IntPtr.Zero,
                error: out error,
                attribute1Type: LinuxKeyRingAttr1.Key,
                attribute1Value: LinuxKeyRingAttr1.Value,
                attribute2Type: LinuxKeyRingAttr2Key,
                attribute2Value: userName,
                end: IntPtr.Zero);

            if (error != IntPtr.Zero)
            {
                try
                {
                    if (Marshal.PtrToStructure(error, typeof(NativeMethods.GError)) is NativeMethods.GError err)
                    {
                        throw new MSStoreException($"Failed to write credential: Domain: '{err.Domain}' - Code: '{err.Code}' - Message: '{err.Message}'");
                    }
                }
                catch (Exception e)
                {
                    throw new MSStoreException("Failed to write credential", e);
                }
            }
        }

        public void ClearCredentials(string userName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ClearCredentialsLinux(userName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ClearCredentialsOSX(userName);
            }
        }

        [SupportedOSPlatform("linux")]
        private void ClearCredentialsLinux(string userName)
        {
            try
            {
                IntPtr error = IntPtr.Zero;

                _ = NativeMethods.secret_password_clear_sync(
                    schema: _libsecretSchema.Value,
                    cancellable: IntPtr.Zero,
                    error: out error,
                    attribute1Type: LinuxKeyRingAttr1.Key,
                    attribute1Value: LinuxKeyRingAttr1.Value,
                    attribute2Type: LinuxKeyRingAttr2Key,
                    attribute2Value: userName,
                    end: IntPtr.Zero);
            }
            catch
            {
            }
        }
    }
}
