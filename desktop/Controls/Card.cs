using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>
    /// A rounded surface panel. Used for cards, stat tiles, list rows and headers.
    /// Supports selective corner rounding so a navy header can sit flush on a white body.
    /// </summary>
    public class Card : Panel
    {
        private int _radius = Theme.Radius;
        private Color _fill = Color.White;
        private Color _border = Theme.Slate150;
        private int _borderWidth = 1;
        private bool _topOnly;
        private bool _bottomOnly;
        private int _leftAccentWidth;
        private Color _leftAccentColor = Theme.Brick600;

        public Card()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        public int Radius
        {
            get { return _radius; }
            set { _radius = Math.Max(0, value); Invalidate(); }
        }

        public Color Fill
        {
            get { return _fill; }
            set { _fill = value; Invalidate(); }
        }

        public Color BorderTint
        {
            get { return _border; }
            set { _border = value; Invalidate(); }
        }

        public int BorderWidth
        {
            get { return _borderWidth; }
            set { _borderWidth = Math.Max(0, value); Invalidate(); }
        }

        /// <summary>Round only the top corners.</summary>
        public bool TopOnly
        {
            get { return _topOnly; }
            set { _topOnly = value; Invalidate(); }
        }

        /// <summary>Round only the bottom corners.</summary>
        public bool BottomOnly
        {
            get { return _bottomOnly; }
            set { _bottomOnly = value; Invalidate(); }
        }

        /// <summary>Width of a solid accent stripe on the left edge (0 for none).</summary>
        public int LeftAccentWidth
        {
            get { return _leftAccentWidth; }
            set { _leftAccentWidth = Math.Max(0, value); Invalidate(); }
        }

        public Color LeftAccentColor
        {
            get { return _leftAccentColor; }
            set { _leftAccentColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            using (GraphicsPath path = Build(r, _radius, _topOnly, _bottomOnly))
            {
                using (SolidBrush b = new SolidBrush(_fill))
                {
                    e.Graphics.FillPath(b, path);
                }

                if (_leftAccentWidth > 0)
                {
                    // Clip to the card outline so the stripe follows the rounded corners.
                    GraphicsState state = e.Graphics.Save();
                    e.Graphics.SetClip(path);
                    using (SolidBrush accent = new SolidBrush(_leftAccentColor))
                    {
                        e.Graphics.FillRectangle(accent, 0, 0, _leftAccentWidth, Height);
                    }
                    e.Graphics.Restore(state);
                }

                if (_borderWidth > 0)
                {
                    using (Pen pen = new Pen(_border, _borderWidth))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            }

            base.OnPaint(e);
        }

        /// <summary>Builds a rounded-rectangle path, optionally rounding only one end.</summary>
        internal static GraphicsPath Build(Rectangle r, int radius, bool topOnly, bool bottomOnly)
        {
            GraphicsPath path = new GraphicsPath();

            if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }

            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;

            bool roundTop = !bottomOnly;
            bool roundBottom = !topOnly;

            if (roundTop)
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            }
            else
            {
                path.AddLine(r.X, r.Y, r.Right, r.Y);
            }

            if (roundBottom)
            {
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            }
            else
            {
                path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            }

            path.CloseFigure();
            return path;
        }
    }
}
