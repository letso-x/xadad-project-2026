using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>
    /// A soft tinted callout banner with a leading icon and a bold "Tip:" style lead-in,
    /// used to guide the user (e.g. "Select a project to view its forms"). Rounded,
    /// full-width, and sizes its height to the wrapped text.
    /// </summary>
    public class InfoBanner : Control
    {
        private string _lead = "Tip:";
        private string _message = string.Empty;
        private Color _fill = Theme.Green050;
        private Color _tint = Theme.Green100;
        private Color _accent = Theme.Green700;
        private Glyph _glyph = Glyph.CheckCircle;

        public InfoBanner()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Dpi.S(44);
        }

        /// <summary>Bold lead-in word, e.g. "Tip:". Set empty to hide.</summary>
        public string Lead
        {
            get { return _lead; }
            set { _lead = value ?? string.Empty; Invalidate(); }
        }

        /// <summary>The guidance message shown after the lead-in.</summary>
        public string Message
        {
            get { return _message; }
            set { _message = value ?? string.Empty; Invalidate(); }
        }

        /// <summary>Switches the palette to a neutral/slate look instead of green.</summary>
        public void UseNeutral()
        {
            _fill = Theme.Slate50;
            _tint = Theme.Slate150;
            _accent = Theme.Navy700;
            Invalidate();
        }

        public Glyph Icon
        {
            get { return _glyph; }
            set { _glyph = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Theme.RadiusSm;

            using (GraphicsPath path = Card.Build(r, radius, false, false))
            using (SolidBrush fill = new SolidBrush(_fill))
            using (Pen border = new Pen(_tint))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int iconSize = Dpi.S(16);
            int iconLeft = Dpi.S(14);
            int iconTop = (Height - iconSize) / 2;
            Glyphs.Draw(g, _glyph, new Rectangle(iconLeft, iconTop, iconSize, iconSize), _accent, 1.8f);

            int textLeft = iconLeft + iconSize + Dpi.S(10);
            int textWidth = Width - textLeft - Dpi.S(14);

            using (StringFormat fmt = new StringFormat())
            {
                fmt.LineAlignment = StringAlignment.Center;
                fmt.Trimming = StringTrimming.EllipsisWord;
                fmt.FormatFlags = StringFormatFlags.NoWrap;

                RectangleF textRect = new RectangleF(textLeft, 0, textWidth, Height);

                float x = textLeft;
                if (!string.IsNullOrEmpty(_lead))
                {
                    using (SolidBrush leadBrush = new SolidBrush(_accent))
                    {
                        g.DrawString(_lead, Theme.SmallBold, leadBrush,
                            new RectangleF(x, 0, textWidth, Height), fmt);
                        x += g.MeasureString(_lead, Theme.SmallBold).Width + Dpi.S(5);
                    }
                }

                using (SolidBrush msgBrush = new SolidBrush(Theme.Slate700))
                {
                    g.DrawString(_message, Theme.Small, msgBrush,
                        new RectangleF(x, 0, Width - x - Dpi.S(14), Height), fmt);
                }
            }
        }
    }
}
