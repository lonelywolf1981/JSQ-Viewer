using Microsoft.VisualStudio.TestTools.UnitTesting;
using JSQViewer.Application.Channels;

namespace JSQViewer.Tests
{
    [TestClass]
    public class NaturalStringComparerTests
    {
        [TestMethod]
        public void Compare_LargeNumericSuffix_SortsNumerically()
        {
            var comparer = NaturalStringComparer.Instance;
            int result = comparer.Compare("CH-2147483647", "CH-2147483648");
            Assert.IsTrue(result < 0,
                "CH-2147483647 должен быть меньше CH-2147483648");
        }

        [TestMethod]
        public void Compare_RegularNumbers_SortsNaturally()
        {
            var comparer = NaturalStringComparer.Instance;
            Assert.IsTrue(comparer.Compare("CH-2", "CH-10") < 0);
            Assert.IsTrue(comparer.Compare("CH-10", "CH-2") > 0);
            Assert.AreEqual(0, comparer.Compare("CH-5", "CH-5"));
        }
    }
}
