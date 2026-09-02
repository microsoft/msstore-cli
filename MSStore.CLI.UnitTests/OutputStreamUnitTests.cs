// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Helpers;
using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class OutputStreamUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddDefaultFakeAccount();
            AddFakeApps();
        }

        [TestCleanup]
        public void ResetOutputStreamEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(EnvironmentInfo.OutputStreamEnvironmentVariable, null);
        }

        [TestMethod]
        public void ResolveDefaultsToStderr()
        {
            var (stream, warning) = OutputStreamResolver.Resolve([], null);

            stream.Should().Be(OutputStream.Stderr);
            warning.Should().BeNull();
        }

        [DataRow("stdout", nameof(OutputStream.Stdout))]
        [DataRow("stderr", nameof(OutputStream.Stderr))]
        [DataRow("STDOUT", nameof(OutputStream.Stdout))]
        [DataRow("StdErr", nameof(OutputStream.Stderr))]
        [TestMethod]
        public void ResolveReadsTheOptionValueCaseInsensitively(string value, string expected)
        {
            var (stream, warning) = OutputStreamResolver.Resolve(["publish", "--output-stream", value], null);

            stream.Should().Be(Enum.Parse<OutputStream>(expected));
            warning.Should().BeNull();
        }

        [DataRow("--output-stream=stdout")]
        [DataRow("--output-stream:stdout")]
        [TestMethod]
        public void ResolveSupportsInlineValueSeparators(string arg)
        {
            var (stream, warning) = OutputStreamResolver.Resolve(["publish", arg], null);

            stream.Should().Be(OutputStream.Stdout);
            warning.Should().BeNull();
        }

        [TestMethod]
        public void ResolveUsesTheLastOccurrenceWhenTheOptionIsRepeated()
        {
            var (stream, _) = OutputStreamResolver.Resolve(
                ["publish", "--output-stream", "stdout", "--output-stream", "stderr"],
                null);

            stream.Should().Be(OutputStream.Stderr);
        }

        [TestMethod]
        public void ResolveIgnoresTheOptionWhenItHasNoValue()
        {
            var (stream, warning) = OutputStreamResolver.Resolve(["publish", "--output-stream"], null);

            stream.Should().Be(OutputStream.Stderr);
            warning.Should().BeNull();
        }

        [TestMethod]
        public void ResolveDoesNotMatchOptionsThatMerelyStartWithTheSameText()
        {
            var (stream, _) = OutputStreamResolver.Resolve(["package", "--output-streamer", "stdout"], null);

            stream.Should().Be(OutputStream.Stderr);
        }

        [TestMethod]
        public void ResolveDoesNotConfuseTheOptionWithTheOutputDirectoryOption()
        {
            var (stream, _) = OutputStreamResolver.Resolve(["package", "--output", "C:\\packages"], null);

            stream.Should().Be(OutputStream.Stderr);
        }

        [TestMethod]
        public void ResolveReadsTheEnvironmentVariableWhenTheOptionIsAbsent()
        {
            var (stream, warning) = OutputStreamResolver.Resolve(["publish"], "stdout");

            stream.Should().Be(OutputStream.Stdout);
            warning.Should().BeNull();
        }

        [DataRow("stdout", "stderr", nameof(OutputStream.Stderr))]
        [DataRow("stderr", "stdout", nameof(OutputStream.Stdout))]
        [TestMethod]
        public void ResolveLetsTheOptionOverrideTheEnvironmentVariable(string environmentValue, string optionValue, string expected)
        {
            var (stream, warning) = OutputStreamResolver.Resolve(
                ["package", "--output-stream", optionValue],
                environmentValue);

            stream.Should().Be(Enum.Parse<OutputStream>(expected));
            warning.Should().BeNull();
        }

        [TestMethod]
        public void ResolveWarnsAndFallsBackWhenTheEnvironmentVariableIsInvalid()
        {
            var (stream, warning) = OutputStreamResolver.Resolve(["publish"], "console");

            stream.Should().Be(OutputStream.Stderr);
            warning.Should().Contain("console");
            warning.Should().Contain(EnvironmentInfo.OutputStreamEnvironmentVariable);
        }

        [DataRow("")]
        [DataRow("   ")]
        [TestMethod]
        public void ResolveTreatsABlankEnvironmentVariableAsUnset(string environmentValue)
        {
            var (stream, warning) = OutputStreamResolver.Resolve(["publish"], environmentValue);

            stream.Should().Be(OutputStream.Stderr);
            warning.Should().BeNull();
        }

        [TestMethod]
        public void ResolveDoesNotWarnForAnInvalidOptionValue()
        {
            // The parser reports invalid option values, so the resolver just falls through.
            var (stream, warning) = OutputStreamResolver.Resolve(["publish", "--output-stream", "console"], null);

            stream.Should().Be(OutputStream.Stderr);
            warning.Should().BeNull();
        }

        [TestMethod]
        public void ResolveReadsTheRealEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(EnvironmentInfo.OutputStreamEnvironmentVariable, "stdout");

            var (stream, warning) = OutputStreamResolver.Resolve(["publish"]);

            stream.Should().Be(OutputStream.Stdout);
            warning.Should().BeNull();
        }

        [DataRow("stdout")]
        [DataRow("stderr")]
        [TestMethod]
        public async Task OutputStreamOptionIsAcceptedByCommands(string value)
        {
            var appId = FakeApps[2].Id!;

            var result = await ParseAndInvokeAsync(
                [
                    "apps",
                    "get",
                    appId,
                    "--output-stream",
                    value
                ]);

            // Machine-readable payloads always go to stdout, whichever stream the human-readable
            // output was routed to.
            result.Output.Should().Contain($"\"Id\": \"{appId}\",");
        }

        [TestMethod]
        public async Task InvalidOutputStreamOptionValueIsRejectedByTheParser()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "apps",
                    "list",
                    "--output-stream",
                    "console"
                ],
                1);

            result.Error.Should().Contain("--output-stream");
        }
    }
}
