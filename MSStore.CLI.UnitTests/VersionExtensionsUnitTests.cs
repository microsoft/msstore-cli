// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Helpers;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class VersionExtensionsUnitTests
    {
        [DataRow(1, 2, null, null, false, "1.2.0.0")]
        [DataRow(1, 2, 3, null, false, "1.2.3.0")]
        [DataRow(1, 2, 3, 4, false, "1.2.3.4")]
        [DataRow(1, 2, null, null, true, "1.2.0")]
        [DataRow(1, 2, 3, null, true, "1.2.3")]
        [DataRow(1, 2, 3, 4, true, "1.2.3")]
        [TestMethod]
        public void ToVersionStringTests(int major, int minor, int? build, int? revision, bool ignoreRevision, string expected)
        {
            Version version;
            if (build == null)
            {
                version = new Version(major, minor);
            }
            else if (revision == null)
            {
                version = new Version(major, minor, build!.Value);
            }
            else
            {
                version = new Version(major, minor, build!.Value, revision!.Value);
            }

            var actual = version.ToVersionString(ignoreRevision);

            Assert.AreEqual(expected, actual);
        }
    }
}
