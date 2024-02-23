// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class FlightsCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddDefaultFakeAccount();
            AddFakeApps();
            AddFakeFlights();
        }

        [TestMethod]
        public async Task FlightsListCommandShouldReturnZero()
        {
            var result = await ParseAndInvokeAsync(
                new string[]
                {
                    "flights",
                    "list",
                    FakeApps[0].Id!
                });

            result.Should().ContainAll(FakeFlights.Select(a => a.FlightId));
            result.Should().ContainAll(FakeFlights.Select(a => a.FriendlyName));
        }
    }
}