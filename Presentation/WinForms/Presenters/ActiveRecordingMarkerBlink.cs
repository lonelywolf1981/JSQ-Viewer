using System;
using JSQViewer.Application.Database;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    /// <summary>
    /// Blinks the "still recording" marker in a window caption. Window captions are drawn by Windows,
    /// so the marker cannot be coloured there — it is blinked instead, by swapping it for blank space.
    /// Two spaces, not one: in the caption font a single space is noticeably narrower than the marker
    /// glyph, which made the rest of the caption twitch left and right on every blink.
    /// </summary>
    public static class ActiveRecordingMarkerBlink
    {
        private const string HiddenMarker = "  ";

        public static string Apply(string caption, bool markerVisible)
        {
            if (string.IsNullOrEmpty(caption) || markerVisible)
            {
                return caption ?? string.Empty;
            }

            return caption.StartsWith(RecordingDisplayNameBuilder.ActiveMarker, StringComparison.Ordinal)
                ? HiddenMarker + caption.Substring(RecordingDisplayNameBuilder.ActiveMarker.Length)
                : caption;
        }
    }
}
