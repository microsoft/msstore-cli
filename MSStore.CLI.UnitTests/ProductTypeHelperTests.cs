// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Helpers;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ProductTypeHelperTests
    {
        [TestMethod]
        public void Test_ProductTypeHelper_Solve_Unpackaged()
        {
            var productId = "12345678-1234-1234-1234-123456789012";
            var productType = ProductTypeHelper.Solve(productId);
            Assert.AreEqual(ProductType.Unpackaged, productType);
        }

        [TestMethod]
        [DataRow("12345678-1234-1234-1234-12345678901")]
        [DataRow("1")]
        [DataRow("1234567890")]
        public void Test_ProductTypeHelper_Solve_Packaged(string productId)
        {
            var productType = ProductTypeHelper.Solve(productId);
            Assert.AreEqual(ProductType.Packaged, productType);
        }
    }
}
