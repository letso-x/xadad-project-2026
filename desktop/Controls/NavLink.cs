using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>A sidebar navigation entry with glyph, hover and active states.</summary>
    public class NavLink : Control
    {
        private bool _active;
        private bool _hovered;
        private bool _collapsed;
        private Glyph _glyph = Glyph.Grid;

        public NavLink()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.NavItem;
            Height = Dpi.S(36);
            Cursor = Cursors.Hand;
        }

        /// <summary>Route key this link navigates to.</summary>
        public string Route { get; set; }

        public bool Active
        {
            get { return _active; }
            set
            {
                _active = value;
                Font = value ? Theme.NavItemActive : Theme.NavItem;
                Invalidate();
            }
        }

        /// <summary>
        /// Icon-only mode for the collapsed sidebar. The label is hidden and the glyph
        /// is centred, with the full text moved to the tooltip.
        /// </summary>
        public bool Collapsed
        {
            get { return _collapsed; }
            set { _collapsed = value; Invalidate(); }
        }

        public Glyph Glyph
        {
            get { return _glyph; }
            set { _glyph = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Paint in 96 DPI design units; PaintScope maps them to device pixels
            // so text and icons render at full resolution instead of being stretched.
            using (PaintScope scope = new PaintScope(g, Width, Height))
            {
                PaintLink(g, scope.W, scope.H);
            }
        }

        private void PaintLink(Graphics g, int Width, int Height)
        {

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            if (_active || _hovered)
            {
                Color fill = _active ? Theme.Brick600 : Color.FromArgb(18, 255, 255, 255);
                using (GraphicsPath path = Card.Build(r, Theme.RadiusSm, false, false))
                using (SolidBrush b = new SolidBrush(fill))
                {
                    g.FillPath(b, path);
                }
            }

            Color fg = _active ? Color.White
                     : (_hovered ? Color.White : Color.FromArgb(174, 189, 208));

            if (_collapsed)
            {
                // Centre the glyph and drop the label entirely.
                Glyphs.Draw(g, _glyph,
                    new Rectangle((Width - 16) / 2, (Height - 16) / 2, 16, 16), fg, 1.7f);
                return;
            }

            Glyphs.Draw(g, _glyph, new Rectangle(11, (Height - 16) / 2, 16, 16), fg, 1.7f);

            using (SolidBrush tb = new SolidBrush(fg))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.LineAlignment = StringAlignment.Center;
                fmt.FormatFlags = StringFormatFlags.NoWrap;
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(Text, Font, tb, new Rectangle(37, 0, Width - 42, Height), fmt);
            }
        }
    }
}
