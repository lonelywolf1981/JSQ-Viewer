using JSQViewer.Presentation.WinForms.Presenters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class ActiveRecordingMarkerBlinkTests
    {
        private const string Lit = "● Post B 2026-08-06 14-33-12 · LIDER · FUNC · 32/65";

        [TestMethod]
        public void Apply_WhenMarkerVisible_KeepsCaptionUnchanged()
        {
            Assert.AreEqual(Lit, ActiveRecordingMarkerBlink.Apply(Lit, true));
        }

        [TestMethod]
        public void Apply_WhenMarkerHidden_ReplacesMarkerWithTwoSpaces()
        {
            string dark = ActiveRecordingMarkerBlink.Apply(Lit, false);

            // Два пробела, а не один: одиночный пробел заметно уже глифа метки, из-за чего
            // остаток заголовка дёргался влево-вправо на каждом мигании.
            Assert.AreEqual("   Post B 2026-08-06 14-33-12 · LIDER · FUNC · 32/65", dark);
        }

        [TestMethod]
        public void Apply_WithoutMarker_LeavesCaptionAlone()
        {
            const string plain = "Post C 2026-08-04 08-53-22 · REX · POWER";

            Assert.AreEqual(plain, ActiveRecordingMarkerBlink.Apply(plain, false));
            Assert.AreEqual(plain, ActiveRecordingMarkerBlink.Apply(plain, true));
        }

        [TestMethod]
        public void Apply_OnlyTouchesTheLeadingMarker()
        {
            const string inner = "Прогон ● особый";

            Assert.AreEqual(inner, ActiveRecordingMarkerBlink.Apply(inner, false));
        }

        [TestMethod]
        public void Apply_WithEmptyCaption_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ActiveRecordingMarkerBlink.Apply(null, false));
            Assert.AreEqual(string.Empty, ActiveRecordingMarkerBlink.Apply(string.Empty, true));
        }
    }
}
