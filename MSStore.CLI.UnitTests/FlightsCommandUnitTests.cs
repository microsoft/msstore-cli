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
                [
                    "flights",
                    "list",
                    FakeApps[0].Id!
                ]);

            result.Should().ContainAll(FakeFlights.Select(a => a.FlightId));
            result.Should().ContainAll(FakeFlights.Select(a => a.FriendlyName));
        }

        [TestMethod]
        public async Task FlightsGetCommandShouldReturnFlight()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "get",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            result.Should().Contain(FakeFlights[0].FlightId);
            result.Should().Contain(FakeFlights[0].FriendlyName);
        }

        [TestMethod]
        public async Task FlightsDeleteCommandShouldDeleteFlight()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "delete",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            result.Should().Contain("Deleted Flight");
        }

        [TestMethod]
        public async Task FlightsCreateCommandShouldCreateFlight()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "create",
                    FakeApps[0].Id!,
                    "NewFlight",
                    "--group-ids",
                    "1",
                ]);

            result.Should().Contain("Created Flight");
            result.Should().Contain("NewFlight");
            result.Should().Contain("632B6A77-0E18-4B41-9033-3614D2174F2E");
        }
    }
}