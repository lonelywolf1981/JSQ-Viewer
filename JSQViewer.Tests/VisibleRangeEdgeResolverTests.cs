using JSQViewer.Application.Charting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class VisibleRangeEdgeResolverTests
    {
        private static readonly long[] Timestamps = { 0L, 1000L, 2000L, 3000L };

        [TestMethod]
        public void ResolveIndex_EdgeExactlyOnSample_ReturnsThatSample()
        {
            Assert.AreEqual(2, VisibleRangeEdgeResolver.ResolveIndex(Timestamps, 2000L));
        }

        [TestMethod]
        public void ResolveIndex_EdgeBetweenSamples_ReturnsEarlierSample()
        {
            Assert.AreEqual(1, VisibleRangeEdgeResolver.ResolveIndex(Timestamps, 1500L));
        }

        [TestMethod]
        public void ResolveIndex_EdgeBeyondLastSample_ReturnsLastSample()
        {
            Assert.AreEqual(3, VisibleRangeEdgeResolver.ResolveIndex(Timestamps, 99999L));
        }

        [TestMethod]
        public void ResolveIndex_EdgeBeforeFirstSample_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, VisibleRangeEdgeResolver.ResolveIndex(Timestamps, -1L));
        }

        [TestMethod]
        public void ResolveIndex_WithEmptyOrNullInput_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, VisibleRangeEdgeResolver.ResolveIndex(new long[0], 1000L));
            Assert.AreEqual(-1, VisibleRangeEdgeResolver.ResolveIndex(null, 1000L));
        }
    }
}
