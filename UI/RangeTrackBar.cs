using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace JSQViewer.UI
{
    public sealed class RangeTrackBar : UserControl
    {
        private double _minimum;
        private double _maximum = 1;
        private double _lowerValue;
        private double _upperValue = 1;

        private const int ThumbWidth = 10;
        private const int TrackHeight = 8;
        private const int TrackPadding = ThumbWidth / 2 + 2;

        private enum DragTarget { None, Lower, Upper, Range }
        private DragTarget _dragging = DragTarget.None;
        private double _dragOffset;

        public event EventHandler RangeChanged;

        public Func<double, string> ValueLabelFormatter { get; set; }

        public double Minimum
        {
            get { return _minimum; }
            set { _minimum = value; Clamp(); Invalidate(); }
        }

        public double Maximum
        {
            get { return _maximum; }
            set { _maximum = value; Clamp(); Invalidate(); }
        }

        public double LowerValue
        {
            get { return _lowerValue; }
            set { _lowerValue = value; Clamp(); Invalidate(); }
        }

        public double UpperValue
        {
            get { return _upperValue; }
            set { _upperValue = value; Clamp(); Invalidate(); }
        }

        public RangeTrackBar()
        {
            Height = 48;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void Clamp()
        {
            if (_lowerValue < _minimum) _lowerValue = _minimum;
            if (_upperValue > _maximum) _upperValue = _maximum;
            if (_lowerValue > _upperValue) _lowerValue = _upperValue;
        }

        private int ValueToPixel(double value)
        {
            double range = _maximum - _minimum;
            if (range <= 0) return TrackPadding;
            double ratio = (value - _minimum) / range;
            return TrackPadding + (int)(ratio * (Width - 2 * TrackPadding));
        }

        private double PixelToValue(int x)
        {
            int trackWidth = Width - 2 * TrackPadding;
            if (trackWidth <= 0) return _minimum;
            double ratio = (double)(x - TrackPadding) / trackWidth;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            return _minimum + ratio * (_maximum - _minimum);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int trackY = Height / 2 - TrackHeight / 2;

            // Background track
            using (var brush = new SolidBrush(Color.FromArgb(220, 220, 220)))
            {
                g.FillRectangle(brush, TrackPadding, trackY, Width - 2 * TrackPadding, TrackHeight);
            }

            int lx = ValueToPixel(_lowerValue);
            int ux = ValueToPixel(_upperValue);

            // Selected range
            using (var brush = new SolidBrush(Color.FromArgb(160, 70, 130, 220)))
            {
                g.FillRectangle(brush, lx, trackY, Math.Max(0, ux - lx), TrackHeight);
            }

            // Lower thumb
            DrawThumb(g, lx, Color.FromArgb(50, 100, 190));
            // Upper thumb
            DrawThumb(g, ux, Color.FromArgb(50, 100, 190));

            // Date labels
            if (_maximum > _minimum)
            {
                try
                {
                    string sLo = FormatValueLabel(_lowerValue);
                    string sHi = FormatValueLabel(_upperValue);
                    using (var font = new Font("Microsoft Sans Serif", 7f))
                    using (var brush = new SolidBrush(Color.FromArgb(80, 80, 80)))
                    {
                        var sfLeft = new StringFormat { Alignment = StringAlignment.Center };
                        int labelY = Height - 16;
                        g.DrawString(sLo, font, brush, lx, labelY, sfLeft);
                        g.DrawString(sHi, font, brush, ux, labelY, sfLeft);
                    }
                }
                catch { }
            }
        }

        private string FormatValueLabel(double value)
        {
            Func<double, string> formatter = ValueLabelFormatter;
            if (formatter != null)
            {
                return formatter(value) ?? string.Empty;
            }

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void DrawThumb(Graphics g, int x, Color color)
        {
            int thumbHeight = Height - 22;
            int thumbY = 2;
            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, x - ThumbWidth / 2, thumbY, ThumbWidth, thumbHeight);
            }
            using (var pen = new Pen(Color.FromArgb(30, 60, 140), 1))
            {
                g.DrawRectangle(pen, x - ThumbWidth / 2, thumbY, ThumbWidth, thumbHeight);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            int lx = ValueToPixel(_lowerValue);
            int ux = ValueToPixel(_upperValue);

            // Check if clicking on a thumb (with some tolerance)
            int tolerance = ThumbWidth / 2 + 4;
            double val = PixelToValue(e.X);

            // Prefer the closer thumb when they overlap
            int distLower = Math.Abs(e.X - lx);
            int distUpper = Math.Abs(e.X - ux);

            if (distLower <= tolerance && distLower <= distUpper)
            {
                _dragging = DragTarget.Lower;
                Capture = true;
            }
            else if (distUpper <= tolerance)
            {
                _dragging = DragTarget.Upper;
                Capture = true;
            }
            else if (e.X > lx + ThumbWidth / 2 && e.X < ux - ThumbWidth / 2)
            {
                // Clicking between thumbs — drag the whole range
                _dragging = DragTarget.Range;
                _dragOffset = val - _lowerValue;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging == DragTarget.None) return;

            double val = PixelToValue(e.X);
            bool changed = false;

            switch (_dragging)
            {
                case DragTarget.Lower:
                    if (val < _minimum) val = _minimum;
                    if (val > _upperValue) val = _upperValue;
                    if (val != _lowerValue) { _lowerValue = val; changed = true; }
                    break;
                case DragTarget.Upper:
                    if (val > _maximum) val = _maximum;
                    if (val < _lowerValue) val = _lowerValue;
                    if (val != _upperValue) { _upperValue = val; changed = true; }
                    break;
                case DragTarget.Range:
                    double rangeWidth = _upperValue - _lowerValue;
                    double newLower = val - _dragOffset;
                    if (newLower < _minimum) newLower = _minimum;
                    if (newLower + rangeWidth > _maximum) newLower = _maximum - rangeWidth;
                    double newUpper = newLower + rangeWidth;
                    if (newLower != _lowerValue || newUpper != _upperValue)
                    {
                        _lowerValue = newLower;
                        _upperValue = newUpper;
                        changed = true;
                    }
                    break;
            }

            if (changed)
            {
                Invalidate();
                OnRangeChanged();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging != DragTarget.None)
            {
                _dragging = DragTarget.None;
                Capture = false;
                OnRangeChanged();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private void OnRangeChanged()
        {
            var h = RangeChanged;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
