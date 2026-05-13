using System;
using System.Drawing;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public static class SourceChannelWindowLayout
    {
        private const int DefaultWidth = 440;
        private const int DefaultHeight = 640;
        private const int HeaderHeight = 78;
        private const int ChannelRowHeight = 17;
        private const int MinimumHeight = 360;
        private const int Margin = 12;
        private const int Gap = 10;
        private const int HorizontalStep = 430;

        public static Rectangle GetBounds(Rectangle workingArea, Rectangle ownerBounds, int index)
        {
            return GetBounds(workingArea, ownerBounds, index, DefaultWidth);
        }

        public static Rectangle GetBounds(Rectangle workingArea, Rectangle ownerBounds, int index, int preferredWidth)
        {
            return GetBounds(workingArea, ownerBounds, index, preferredWidth, 0);
        }

        public static Rectangle GetBounds(Rectangle workingArea, Rectangle ownerBounds, int index, int preferredWidth, int channelCount)
        {
            if (workingArea.Width <= 0 || workingArea.Height <= 0)
            {
                return new Rectangle(0, 0, NormalizeWidth(preferredWidth, DefaultWidth), ResolvePreferredHeight(channelCount));
            }

            int width = Math.Min(NormalizeWidth(preferredWidth, DefaultWidth), Math.Max(1, workingArea.Width - (Margin * 2)));
            int height = Math.Min(ResolvePreferredHeight(channelCount), Math.Max(1, workingArea.Height - (Margin * 2)));
            int step = Math.Max(1, Math.Min(HorizontalStep, width + Gap));
            int columns = Math.Max(1, ((workingArea.Width - (Margin * 2) - width) / step) + 1);
            int safeIndex = Math.Max(0, index);
            int column = safeIndex % columns;
            int row = safeIndex / columns;

            int x = workingArea.Left + Margin + (column * step);
            int preferredY = ownerBounds.Bottom + Gap + (row * Gap);

            return ClampToWorkingArea(new Rectangle(x, preferredY, width, height), workingArea);
        }

        private static int ResolvePreferredHeight(int channelCount)
        {
            if (channelCount <= 0)
            {
                return DefaultHeight;
            }

            int calculated = HeaderHeight + (channelCount * ChannelRowHeight);
            return Math.Max(MinimumHeight, calculated);
        }

        private static int NormalizeWidth(int width, int fallback)
        {
            if (width < 360)
            {
                return fallback;
            }

            return Math.Min(width, 900);
        }

        private static Rectangle ClampToWorkingArea(Rectangle bounds, Rectangle workingArea)
        {
            int x = Math.Max(workingArea.Left, Math.Min(bounds.Left, workingArea.Right - bounds.Width));
            int y = Math.Max(workingArea.Top, Math.Min(bounds.Top, workingArea.Bottom - bounds.Height));
            return new Rectangle(x, y, bounds.Width, bounds.Height);
        }
    }
}
