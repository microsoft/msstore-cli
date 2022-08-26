// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// https://github.com/dotnet/roslyn-analyzers/issues/5479
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments

namespace MSStore.CLI.Services.CredentialManager.Unix
{
    internal static class NativeMethods
    {
        [SupportedOSPlatform("macos")]
        private const string LibSecret = "libsecret-1.so.0";

        [SupportedOSPlatform("linux")]
        internal enum SecretSchemaAttributeType
        {
            SECRET_SCHEMA_ATTRIBUTE_STRING = 0
        }

        [SupportedOSPlatform("linux")]
        public enum SecretSchemaFlags
        {
            SECRET_SCHEMA_NONE = 0,
            SECRET_SCHEMA_DONT_MATCH_NAME = 1 << 1
        }

        [SupportedOSPlatform("linux")]
        internal struct GError
        {
            public uint Domain;
            public int Code;
            public string Message;
        }

        [SupportedOSPlatform("linux")]
        [DllImport(LibSecret, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Auto)]
        public static extern IntPtr secret_schema_new(string name, int flags, string attribute1, int attribute1Type, string attribute2, int attribute2Type, IntPtr end);

        [SupportedOSPlatform("linux")]
        [DllImport(LibSecret, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Auto)]
        public static extern int secret_password_store_sync(IntPtr schema, string collection, string label, string password, IntPtr cancellable, out IntPtr error, string attribute1Type, string attribute1Value, string attribute2Type, string attribute2Value, IntPtr end);

        [SupportedOSPlatform("linux")]
        [DllImport(LibSecret, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Auto)]
        public static extern string secret_password_lookup_sync(IntPtr schema, IntPtr cancellable, out IntPtr error, string attribute1Type, string attribute1Value, string attribute2Type, string attribute2Value, IntPtr end);

        [SupportedOSPlatform("linux")]
        [DllImport(LibSecret, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Auto)]
        public static extern int secret_password_clear_sync(IntPtr schema, IntPtr cancellable, out IntPtr error, string attribute1Type, string attribute1Value, string attribute2Type, string attribute2Value, IntPtr end);

        [SupportedOSPlatform("macos")]
        private const string FoundationFramework = "/System/Library/Frameworks/Foundation.framework/Foundation";
        [SupportedOSPlatform("macos")]
        private const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";

        [SupportedOSPlatform("macos")]
        public class SecResultCodes
        {
            public const int ErrSecSuccess = 0;
            public const int ErrSecItemNotFound = -25300;
        }

        [SupportedOSPlatform("macos")]
        [DllImport(FoundationFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CFRelease(IntPtr handle);

        [SupportedOSPlatform("macos")]
        [DllImport(SecurityFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

        [SupportedOSPlatform("macos")]
        [DllImport(SecurityFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SecKeychainFindGenericPassword(IntPtr keychainOrArray, uint serviceNameLength, string serviceName, uint accountNameLength, string accountName, out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef);

        [SupportedOSPlatform("macos")]
        [DllImport(SecurityFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceNameLength, string serviceName, uint accountNameLength, string accountName, uint passwordLength, byte[] passwordData, out IntPtr itemRef);

        [SupportedOSPlatform("macos")]
        [DllImport(SecurityFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SecKeychainItemModifyAttributesAndData(IntPtr itemRef, IntPtr attrList, uint passwordLength, byte[] passwordData);

        [SupportedOSPlatform("macos")]
        [DllImport(SecurityFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SecKeychainItemDelete(IntPtr itemRef);
    }
}

#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments