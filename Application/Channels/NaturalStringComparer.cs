using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace JSQViewer.Application.Channels
{
    internal sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

        private static readonly Regex SplitRegex = new Regex("(\\d+)", RegexOptions.Compiled);

        public int Compare(string x, string y)
        {
            string[] a = SplitRegex.Split(x ?? string.Empty);
            string[] b = SplitRegex.Split(y ?? string.Empty);
            int count = Math.Max(a.Length, b.Length);
            for (int i = 0; i < count; i++)
            {
                if (i >= a.Length) return -1;
                if (i >= b.Length) return 1;
                int ai, bi;
                bool aIsNum = int.TryParse(a[i], out ai);
                bool bIsNum = int.TryParse(b[i], out bi);
                int cmp = (aIsNum && bIsNum)
                    ? ai.CompareTo(bi)
                    : string.Compare(a[i], b[i], StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
            }
            return 0;
        }
    }
}
