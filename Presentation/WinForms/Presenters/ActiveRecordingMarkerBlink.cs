using System;
using JSQViewer.Application.Database;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    /// <summary>
    /// Blinks the "still recording" marker in a window caption. Window captions are drawn by Windows,
    /// so the marker cannot be coloured there — it is blinked instead, by swapping it for a space of
    /// the same position so the rest of the caption does not shift.
    /// </summary>
    public static class ActiveRecordingMarkerBlink
    {
        public static string Apply(string caption, bool markerVisible)
        {
            if (string.IsNullOrEmpty(caption) || markerVisible)
            {
                return caption ?? string.Empty;
            }

            return caption.StartsWith(RecordingDisplayNameBuilder.ActiveMarker, StringComparison.Ordinal)
                ? " " + caption.Substring(RecordingDisplayNameBuilder.ActiveMarker.Length)
                : caption;
        }
    }
}
