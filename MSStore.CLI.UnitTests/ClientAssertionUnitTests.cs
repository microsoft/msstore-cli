// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ClientAssertionUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        [TestCleanup]
        public void ClearAssertionVars()
        {
            Environment.SetEnvironmentVariable("MSSTORE_CLIENT_ASSERTION", null);
            Environment.SetEnvironmentVariable("MSSTORE_CLIENT_ASSERTION_FILE", null);
        }

        [TestMethod]
        public async Task NoVariablesShouldFail()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(EnvironmentInfo.GetClientAssertionAsync);
        }

        [TestMethod]
        public async Task BothVariablesShouldFail()
        {
            Environment.SetEnvironmentVariable("MSSTORE_CLIENT_ASSERTION", "test");
            Environment.SetEnvironmentVariable("MSSTORE_CLIENT_ASSERTION_FILE", "test");
            await Assert.ThrowsAsync<InvalidOperationException>(EnvironmentInfo.GetClientAssertionAsync);
        }

        [TestMethod]
        public async Task VariableShouldReturn()
        {
            const string testValue = "test";
            Environment.SetEnvironmentVariable("MSSTORE_CLIENT_ASSERTION", testValue);
            Assert.AreEqual(testValue, await EnvironmentInfo.GetClientAssertionAsync());
        }

        [TestMethod]
        public async Task FileShouldTrim()
        {
            var path = CopyFilesRecursively("ClientAssertion");
            Environment.SetEnvironmentVariable("MSSTORE_CLIENT_ASSERTION_FILE", Path.Combine(path, "clientassertion.txt"));
            Assert.AreEqual("test", await EnvironmentInfo.GetClientAssertionAsync());
        }
    }
}