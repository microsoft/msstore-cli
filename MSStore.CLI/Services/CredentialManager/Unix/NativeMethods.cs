// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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

        /*
         * Remove after when dotnet8 ships: https://github.com/dotnet/docs/issues/31423
         * By then, also check if unsafe flag can be removed.
         */
        private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        [SupportedOSPlatform("macos")]

        [SupportedOSPlatform("macos")]
        public enum NSSearchPathDirectory : ulong
        {
            ApplicationDirectory = 1,
            DemoApplicationDirectory,
            DeveloperApplicationDirectory,
            AdminApplicationDirectory,
            LibraryDirectory,
            DeveloperDirectory,
            UserDirectory,
            DocumentationDirectory,
            DocumentDirectory,
            CoreServiceDirectory,
            AutosavedInformationDirectory = 11,
            DesktopDirectory = 12,
            CachesDirectory = 13,
            ApplicationSupportDirectory = 14,
            DownloadsDirectory = 15,
            InputMethodsDirectory = 16,
            MoviesDirectory = 17,
            MusicDirectory = 18,
            PicturesDirectory = 19,
            PrinterDescriptionDirectory = 20,
            SharedPublicDirectory = 21,
            PreferencePanesDirectory = 22,
            ApplicationScriptsDirectory = 23,
            ItemReplacementDirectory = 99,
            AllApplicationsDirectory = 100,
            AllLibrariesDirectory = 101,
            TrashDirectory = 102,
        }

        [SupportedOSPlatform("macos")]
        [Flags]
        public enum NSSearchPathDomain : ulong
        {
            None = 0,
            User = 1 << 0,
            Local = 1 << 1,
            Network = 1 << 2,
            System = 1 << 3,
            All = 0x0ffff,
        }

        [SupportedOSPlatform("macos")]
        public static string?[]? GetDirectories(NSSearchPathDirectory directory, NSSearchPathDomain domainMask, bool expandTilde = true)
        {
            return ArrayFromHandleFunc(NSSearchPathForDirectoriesInDomains((nuint)(ulong)directory, (nuint)(ulong)domainMask, expandTilde), CFStringFromHandle);
        }

        [SupportedOSPlatform("macos")]
        [DllImport(FoundationFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NSSearchPathForDirectoriesInDomains(nuint directory, nuint domainMask, [MarshalAs(UnmanagedType.I1)] bool expandTilde);

        [SupportedOSPlatform("macos")]
        [DllImport(CoreFoundationFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern nint CFStringGetLength(IntPtr handle);

        [SupportedOSPlatform("macos")]
        [DllImport(CoreFoundationFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe char* CFStringGetCharactersPtr(IntPtr handle);

        [SupportedOSPlatform("macos")]
        [DllImport(CoreFoundationFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe IntPtr CFStringGetCharacters(IntPtr handle, CFRange range, char* buffer);

        [SupportedOSPlatform("macos")]
        [DllImport(CoreFoundationFramework, EntryPoint = "CFArrayGetCount", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern /* CFIndex */ nint GetCount(/* CFArrayRef */ IntPtr theArray);

        [SupportedOSPlatform("macos")]
        [DllImport(CoreFoundationFramework, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CFArrayGetValues(/* CFArrayRef */ IntPtr theArray, CFRange range, /* const void ** */ IntPtr values);

        [SupportedOSPlatform("macos")]
        private static T?[]? ArrayFromHandleFunc<T>(IntPtr handle, Func<IntPtr, T> createObject)
        {
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var c = (int)GetCount(handle);
            if (c == 0)
            {
                return [];
            }

            var buffer = c <= 256 ? stackalloc IntPtr[c] : new IntPtr[c];
            unsafe
            {
                fixed (void* ptr = buffer)
                {
                    CFArrayGetValues(handle, new CFRange(0, c), (IntPtr)ptr);
                }
            }

            var ret = new T[c];
            for (var i = 0; i < c; i++)
            {
                ret[i] = createObject(buffer[i]);
            }

            return ret;
        }

        [SupportedOSPlatform("macos")]
        [StructLayout(LayoutKind.Sequential)]
        private struct CFRange
        {
#pragma warning disable IDE0044 // Add readonly modifier
            private nint loc; // defined as 'long' in native code
            private nint len; // defined as 'long' in native code
#pragma warning restore IDE0044 // Add readonly modifier

            public readonly int Location => (int)loc;

            public readonly int Length => (int)len;

            public readonly long LongLocation => loc;

            public readonly long LongLength => len;

            public CFRange(int loc, int len)
            {
                this.loc = loc;
                this.len = len;
            }

            public CFRange(long l, long len)
            {
                loc = (nint)l;
                this.len = (nint)len;
            }

            public CFRange(nint l, nint len)
            {
                loc = l;
                this.len = len;
            }

            public override readonly string ToString()
            {
                return $"CFRange [Location: {loc} Length: {len}]";
            }
        }

        [SupportedOSPlatform("macos")]
        private static string? CFStringFromHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            int l = (int)CFStringGetLength(handle);
            if (l == 0)
            {
                return string.Empty;
            }

            string str;
            bool allocate_memory = false;
            CFRange r = new CFRange(0, l);
            unsafe
            {
                // this returns non-null only if the string can be represented as unicode
                char* u = CFStringGetCharactersPtr(handle);
                if (u is null)
                {
                    // alloc short string on the stack, otherwise use the heap
                    allocate_memory = l > 128;

                    // var m = allocate_memory ? (char*) Marshal.AllocHGlobal (l * 2) : stackalloc char [l];
                    // this ^ won't compile so...
                    if (allocate_memory)
                    {
                        u = (char*)Marshal.AllocHGlobal(l * 2);
                    }
                    else
                    {
                        // `u = stackalloc char [l];` won't compile either, even with cast
                        char* u2 = stackalloc char[l];
                        u = u2;
                    }

                    CFStringGetCharacters(handle, r, u);
                }

                str = new string(u, 0, l);
                if (allocate_memory)
                {
                    Marshal.FreeHGlobal((IntPtr)u);
                }
            }

            return str;
        }
    }
}