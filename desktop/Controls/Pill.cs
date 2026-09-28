using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>
    /// A rounded status/role badge with an optional leading glyph. Sizes itself to its
    /// text when <see cref="AutoFit"/> is called.
    /// </summary>
    public class Pill : Control
    {
        private Color _fill = Theme.Slate150;
        private Color _textColor = Theme.Slate800;
        private Glyph _glyph = Glyph.None;

        public Pill()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.TinyBold;
            Height = Dpi.S(22);
        }

        public Color Fill
        {
            get { return _fill; }
            set { _fill = value; Invalidate(); }
        }

        public Color TextColor
        {
            get { return _textColor; }
            set { _textColor = value; Invalidate(); }
        }

        public Glyph Glyph
        {
            get { return _glyph; }
            set { _glyph = value; Invalidate(); }
        }

        /// <summary>Draws a plain circular dot instead of a glyph.</summary>
        public bool ShowDot { get; set; }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Paint in 96 DPI design units; PaintScope maps them to device pixels
            // so text and icons render at full resolution instead of being stretched.
            using (PaintScope scope = new PaintScope(g, Width, Height))
            {
                PaintPill(g, scope.W, scope.H);
            }
        }

        private void PaintPill(Graphics g, int Width, int Height)
        {

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            using (GraphicsPath path = Card.Build(r, Math.Max(1, r.Height / 2), false, false))
            using (SolidBrush b = new SolidBrush(_fill))
            {
                g.FillPath(b, path);
            }

            int left = 9;

            if (ShowDot)
            {
                const int dot = 7;
                using (SolidBrush db = new SolidBrush(_textColor))
                {
                    g.FillEllipse(db, left, (Height - dot) / 2, dot, dot);
                }
                left += dot + 5;
            }
            else if (_glyph != Glyph.None)
            {
                int size = 12;
                Glyphs.Draw(g, _glyph, new Rectangle(left, (Height - size) / 2, size, size), _textColor, 1.7f);
                left += size + 4;
            }

            using (SolidBrush tb = new SolidBrush(_textColor))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.LineAlignment = StringAlignment.Center;
                fmt.FormatFlags = StringFormatFlags.NoWrap;
                // No trimming/ellipsis: AutoFit guarantees the width, so draw the text
                // in full. Reserve a symmetric right margin equal to the left inset.
                int right = left;
                g.DrawString(Text, Font, tb,
                    new RectangleF(left, 0, Math.Max(1, Width - left - right), Height), fmt);
            }
        }

        /// <summary>Resizes to fit the current text plus any leading glyph.</summary>
        public void AutoFit()
        {
            using (Graphics g = CreateGraphics())
            {
                // MeasureString here returns the device-pixel text width (the font
                // renders DPI-scaled). The paint places text at left=9 design units and
                // reserves 6 on the right, so mirror those in device units and add a
                // little slack so nothing sits against the rounded edge.
                // Measure with the same string format the paint uses so the widths
                // agree, and round up generously. GDI+ MeasureString slightly
                // under-reports tight bold text, so add a full character of slack.
                SizeF measured;
                using (StringFormat fmt = new StringFormat(StringFormat.GenericTypographic))
                {
                    fmt.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
                    measured = g.MeasureString(Text, Font, int.MaxValue, fmt);
                }
                int textWidth = (int)Math.Ceiling(measured.Width);

                int leftPad = ShowDot ? Dpi.S(9 + 7 + 5)
                            : (_glyph != Glyph.None ? Dpi.S(9 + 12 + 4) : Dpi.S(12));
                int rightPad = Dpi.S(12);

                // Extra slack (~one bold character) absorbs measurement rounding and the
                // PaintScope floor, so text never reaches the rounded edge.
                Width = textWidth + leftPad + rightPad + Dpi.S(8);
            }
        }

        /// <summary>Applies the palette for a record status and fits the text.</summary>
        public void ApplyStatus(Models.RecordStatus status)
        {
            Text = Format.StatusText(status);
            Fill = Format.StatusBack(status);
            TextColor = Format.StatusFore(status);
            Glyph = status == Models.RecordStatus.InProgress ? Glyph.Clock
                  : status == Models.RecordStatus.CompleteAccepted ? Glyph.CheckCircle
                  : Glyph.Alert;
            AutoFit();
        }
    }
}
