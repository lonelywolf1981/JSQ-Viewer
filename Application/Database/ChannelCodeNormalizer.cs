using System;

namespace JSQViewer.Application.Database
{
    public static class ChannelCodeNormalizer
    {
        public static string StripPostPrefix(string channelId, string postId)
        {
            string code = (channelId ?? string.Empty).Trim();
            if (code.Length == 0 || string.IsNullOrWhiteSpace(postId))
            {
                return code;
            }

            string prefix = postId.Trim() + "-";
            if (code.Length > prefix.Length && code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return code.Substring(prefix.Length);
            }

            return code;
        }
    }
}
