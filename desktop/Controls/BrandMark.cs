using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>The square "MZ" brand mark: brick fill, white monospace initials.</summary>
    public class BrandMark : Control
    {
        private Color _fill = Theme.Brick600;

        public BrandMark()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Consolas", 9.5F, FontStyle.Bold);
            Size = new Size(Dpi.S(32), Dpi.S(32));
            Text = "MZ";
        }

        public Color Fill
        {
            get { return _fill; }
            set { _fill = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Paint in 96 DPI design units; PaintScope maps them to device pixels
            // so text and icons render at full resolution instead of being stretched.
            using (PaintScope scope = new PaintScope(g, Width, Height))
            {
                PaintMark(g, scope.W, scope.H);
            }
        }

        private void PaintMark(Graphics g, int Width, int Height)
        {

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            using (GraphicsPath path = Card.Build(r, Theme.RadiusSm, false, false))
            using (SolidBrush b = new SolidBrush(_fill))
            {
                g.FillPath(b, path);
            }

            using (SolidBrush tb = new SolidBrush(ForeColor))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Center;
                g.DrawString(Text, Font, tb, r, fmt);
            }
        }
    }

    /// <summary>A circular avatar showing a user's initials.</summary>
    public class Avatar : Control
    {
        public Avatar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = Theme.TinyBold;
            Size = new Size(Dpi.S(32), Dpi.S(32));
        }

        public Color Fill { get; set; } = Theme.Navy700;
        public Color Ring { get; set; } = Color.FromArgb(38, 255, 255, 255);

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            using (SolidBrush b = new SolidBrush(Fill))
            {
                g.FillEllipse(b, r);
            }
            using (Pen pen = new Pen(Ring))
            {
                g.DrawEllipse(pen, r);
            }

            using (SolidBrush tb = new SolidBrush(ForeColor))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Center;
                g.DrawString(Text, Font, tb, r, fmt);
            }
        }
    }
}
