using System;

namespace JSQViewer.Application.Database
{
    public static class RecordingSourceRef
    {
        public const string Scheme = "jsqdb://recording/";

        public static string Build(string recordingId)
        {
            return Scheme + (recordingId ?? string.Empty).Trim();
        }

        public static bool IsRecordingSource(string source)
        {
            string recordingId;
            return TryParse(source, out recordingId);
        }

        public static bool TryParse(string source, out string recordingId)
        {
            recordingId = null;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            string trimmed = source.Trim().Trim('"');
            if (!trimmed.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string id = trimmed.Substring(Scheme.Length).Trim();
            if (id.Length == 0)
            {
                return false;
            }

            recordingId = id;
            return true;
        }
    }
}
