using System.Drawing;

namespace JSQViewer.Presentation.WinForms.Charting
{
    /// <summary>
    /// Цвета линий статистики T8+ по номеру источника. Своя палитра, а не палитра
    /// каналов: та назначается MS Chart автоматически, занимать её нельзя.
    /// </summary>
    public static class SourceColorPalette
    {
        private static readonly Color[] Colors =
        {
            Color.FromArgb(255, 20, 20, 20),
            Color.FromArgb(255, 200, 30, 30),
            Color.FromArgb(255, 20, 90, 200),
            Color.FromArgb(255, 20, 130, 60),
            Color.FromArgb(255, 150, 40, 170),
            Color.FromArgb(255, 200, 110, 0)
        };

        public static int Count
        {
            get { return Colors.Length; }
        }

        public static Color ForSourceIndex(int index)
        {
            // Отрицательный индекс не может возникнуть в штатной работе конвейера
            // (SourceIndex формируется из неотрицательного порядкового номера
            // источника), поэтому для него не выполняется циклическая обёртка —
            // достаточно безопасного значения по умолчанию (первый цвет палитры).
            if (index < 0)
            {
                return Colors[0];
            }

            return Colors[index % Colors.Length];
        }
    }
}
