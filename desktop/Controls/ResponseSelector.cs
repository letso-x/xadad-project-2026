using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MzuApplication.Models;

namespace MzuApplication.Controls
{
    /// <summary>
    /// The A / R / N-A segmented control for one checklist line. Clicking the selected
    /// option clears it, matching the toggle behaviour in the spec.
    /// </summary>
    public class ResponseSelector : Control
    {
        private ItemResponse _value = ItemResponse.Unanswered;
        private int _hoverIndex = -1;

        private static readonly ItemResponse[] Order =
        {
            ItemResponse.Accepted,
            ItemResponse.Rejected,
            ItemResponse.NotApplicable
        };

        public ResponseSelector()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Dpi.S(176), Dpi.S(36));
            Cursor = Cursors.Hand;
        }

        /// <summary>Raised when the user changes the response.</summary>
        public event EventHandler ValueChanged;

        public ItemResponse Value
        {
            get { return _value; }
            set { _value = value; Invalidate(); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int index = IndexAt(e.X);
            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            int index = IndexAt(e.X);
            if (index < 0) return;

            ItemResponse clicked = Order[index];
            _value = _value == clicked ? ItemResponse.Unanswered : clicked;

            Invalidate();

            EventHandler handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private int IndexAt(int x)
        {
            int segment = Width / 3;
            if (segment <= 0) return -1;

            int index = x / segment;
            return index < 0 || index > 2 ? -1 : index;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Paint in 96 DPI design units; PaintScope maps them to device pixels
            // so text and icons render at full resolution instead of being stretched.
            using (PaintScope scope = new PaintScope(g, Width, Height))
            {
                PaintSelector(g, scope.W, scope.H);
            }
        }

        private void PaintSelector(Graphics g, int Width, int Height)
        {

            int gap = 6;
            int segment = (Width - gap * 2) / 3;

            for (int i = 0; i < 3; i++)
            {
                ItemResponse option = Order[i];
                bool selected = _value == option;
                bool hovered = _hoverIndex == i;

                Rectangle r = new Rectangle(i * (segment + gap), 0, segment, Height - 1);
                if (r.Width <= 0 || r.Height <= 0) continue;

                Color fill, border, text;

                if (selected)
                {
                    fill = option == ItemResponse.Accepted ? Theme.Green600
                         : option == ItemResponse.Rejected ? Theme.Red600
                         : Theme.Slate600;
                    border = fill;
                    text = Color.White;
                }
                else
                {
                    fill = Color.White;
                    border = hovered ? Theme.Slate400 : Theme.Slate200;
                    text = hovered ? Theme.Slate700 : Theme.Slate500;
                }

                using (GraphicsPath path = Card.Build(r, Theme.RadiusSm, false, false))
                using (SolidBrush fb = new SolidBrush(fill))
                using (Pen pen = new Pen(border, 1.5f))
                {
                    g.FillPath(fb, path);
                    g.DrawPath(pen, path);
                }

                // Glyph plus code, centred as a unit.
                string label = Format.ResponseCode(option);
                Glyph glyph = option == ItemResponse.Accepted ? Glyph.Check
                            : option == ItemResponse.Rejected ? Glyph.Cross
                            : Glyph.None;

                using (Font f = Theme.SmallBold)
                {
                    SizeF size = g.MeasureString(label, f);
                    int glyphWidth = glyph == Glyph.None ? 0 : 13;
                    int spacing = glyphWidth > 0 ? 5 : 0;
                    int contentWidth = glyphWidth + spacing + (int)Math.Ceiling(size.Width);
                    int startX = r.X + Math.Max(4, (r.Width - contentWidth) / 2);

                    if (glyph != Glyph.None)
                    {
                        Glyphs.Draw(g, glyph,
                            new Rectangle(startX, (Height - 13) / 2, 13, 13), text, 2f);
                    }

                    using (SolidBrush tb = new SolidBrush(text))
                    using (StringFormat fmt = new StringFormat())
                    {
                        fmt.LineAlignment = StringAlignment.Center;
                        fmt.FormatFlags = StringFormatFlags.NoWrap;
                        g.DrawString(label, f, tb,
                            new Rectangle(startX + glyphWidth + spacing, 0,
                                r.Right - startX - glyphWidth - spacing, Height), fmt);
                    }
                }
            }
        }
    }
}
