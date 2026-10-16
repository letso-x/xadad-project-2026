using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>A slim rounded progress track.</summary>
    public class ProgressTrack : Control
    {
        private int _percent;
        private Color _bar = Theme.Navy700;

        public ProgressTrack()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Dpi.S(7);
        }

        public int Percent
        {
            get { return _percent; }
            set { _percent = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public Color BarColor
        {
            get { return _bar; }
            set { _bar = value; Invalidate(); }
        }

        /// <summary>Picks green at 100%, red when flagged, amber otherwise.</summary>
        public void ApplyTone(bool hasRejected)
        {
            BarColor = _percent >= 100 ? Theme.Green600
                     : hasRejected ? Theme.Red600
                     : Theme.Amber600;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Paint in 96 DPI design units; PaintScope maps them to device pixels
            // so text and icons render at full resolution instead of being stretched.
            using (PaintScope scope = new PaintScope(g, Width, Height))
            {
                PaintTrack(g, scope.W, scope.H);
            }
        }

        private void PaintTrack(Graphics g, int Width, int Height)
        {

            int radius = Math.Max(1, Height / 2);
            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            if (track.Width <= 0 || track.Height <= 0) return;

            using (GraphicsPath path = Card.Build(track, radius, false, false))
            using (SolidBrush b = new SolidBrush(Theme.Slate100))
            {
                g.FillPath(b, path);
            }

            if (_percent <= 0) return;

            int w = (int)Math.Round((Width - 1) * (_percent / 100.0));
            if (w < Height) w = Height;

            using (GraphicsPath path = Card.Build(new Rectangle(0, 0, w, Height - 1), radius, false, false))
            using (SolidBrush b = new SolidBrush(_bar))
            {
                g.FillPath(b, path);
            }
        }
    }
}
