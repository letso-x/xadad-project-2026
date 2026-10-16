using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>Visual variants matching the .btn CSS classes.</summary>
    public enum ButtonVariant
    {
        /// <summary>White surface, slate border.</summary>
        Default,

        /// <summary>Navy fill, white text.</summary>
        Primary,

        /// <summary>Brick fill, white text.</summary>
        Brick,

        /// <summary>Transparent until hovered.</summary>
        Ghost,

        /// <summary>Red tinted, for destructive actions.</summary>
        Danger
    }

    /// <summary>
    /// A flat button with an optional leading glyph, hover feedback and the variants
    /// used across the app.
    /// </summary>
    public class FlatButton : Control
    {
        private ButtonVariant _variant = ButtonVariant.Default;
        private Glyph _glyph = Glyph.None;
        private bool _hovered;
        private bool _pressed;
        private bool _enabledLook = true;
        private bool _allowGrow = true;
        private ContentAlignment _align = ContentAlignment.MiddleCenter;

        public FlatButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.SmallBold;
            Height = Dpi.S(34);
            Cursor = Cursors.Hand;
        }

        public ButtonVariant Variant
        {
            get { return _variant; }
            set { _variant = value; Invalidate(); }
        }

        /// <summary>
        /// Sets the button's width to fit its glyph and label with comfortable
        /// padding, clamped to <paramref name="minWidth"/> and <paramref name="maxWidth"/>.
        /// Prevents labels being clipped when the text is longer than a fixed width
        /// would allow — which is the enterprise-correct behaviour for action buttons.
        /// </summary>
        public void FitToText(int minWidth = 0, int maxWidth = int.MaxValue)
        {
            using (Graphics g = CreateGraphics())
            {
                int textWidth = (int)Math.Ceiling(g.MeasureString(Text, Font).Width);
                int glyph = _glyph == Glyph.None ? 0 : Dpi.S(15) + Dpi.S(7);
                // Symmetric padding for centred buttons; a little more on the left for
                // left-aligned ones so the glyph is not jammed against the edge.
                int padding = LeftAlign ? Dpi.S(28) : Dpi.S(34);

                int desired = textWidth + glyph + padding;
                if (desired < minWidth) desired = minWidth;
                if (desired > maxWidth) desired = maxWidth;

                Width = desired;
            }
        }

        public Glyph Glyph
        {
            get { return _glyph; }
            set { _glyph = value; EnsureFits(); Invalidate(); }
        }

        /// <summary>
        /// When true (the default) the button never lets its assigned width fall below
        /// what the glyph and label need, so text can never be clipped. Equal-width
        /// grid layouts set this false when they manage sizing themselves.
        /// </summary>
        public bool AllowGrow
        {
            get { return _allowGrow; }
            set { _allowGrow = value; if (value) EnsureFits(); }
        }

        /// <summary>The minimum width this button needs to show its content in full.</summary>
        private int ContentWidth()
        {
            if (string.IsNullOrEmpty(Text)) return 0;

            using (Graphics g = CreateGraphics())
            {
                int textWidth = (int)Math.Ceiling(g.MeasureString(Text, Font).Width);
                int glyph = _glyph == Glyph.None ? 0 : Dpi.S(15) + Dpi.S(7);
                int padding = _align == ContentAlignment.MiddleLeft ? Dpi.S(30) : Dpi.S(34);
                return textWidth + glyph + padding;
            }
        }

        /// <summary>Grows the button if its current width would clip the content.</summary>
        private void EnsureFits()
        {
            if (!_allowGrow || !IsHandleCreated) return;

            int needed = ContentWidth();
            if (needed > Width) Width = needed;
        }

        /// <summary>Left-align the label instead of centring it.</summary>
        public bool LeftAlign
        {
            get { return _align == ContentAlignment.MiddleLeft; }
            set { _align = value ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter; Invalidate(); }
        }

        /// <summary>Greys the button and blocks clicks.</summary>
        public bool Actionable
        {
            get { return _enabledLook; }
            set
            {
                _enabledLook = value;
                Cursor = value ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
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
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _pressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            // Swallow clicks when the button is presented as unavailable.
            if (!_enabledLook) return;
            base.OnClick(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            EnsureFits();
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Handle now exists, so MeasureString works; enforce the minimum width.
            EnsureFits();
        }

        public override Font Font
        {
            get { return base.Font; }
            set { base.Font = value; EnsureFits(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Paint in 96 DPI design units; PaintScope maps them to device pixels
            // so text and icons render at full resolution instead of being stretched.
            using (PaintScope scope = new PaintScope(g, Width, Height))
            {
                PaintButton(g, scope.W, scope.H);
            }
        }

        private void PaintButton(Graphics g, int Width, int Height)
        {

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            Color fill, border, text;
            ResolveColors(out fill, out border, out text);

            using (GraphicsPath path = Card.Build(r, Theme.RadiusSm, false, false))
            {
                if (fill != Color.Transparent)
                {
                    using (SolidBrush b = new SolidBrush(fill))
                    {
                        g.FillPath(b, path);
                    }
                }

                if (border != Color.Transparent)
                {
                    using (Pen pen = new Pen(border))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            // Measure so glyph + label can be centred as a unit.
            SizeF textSize = g.MeasureString(Text, Font);
            int glyphSize = _glyph == Glyph.None ? 0 : 15;
            int gap = glyphSize > 0 ? 7 : 0;
            int contentWidth = glyphSize + gap + (int)Math.Ceiling(textSize.Width);

            int startX = _align == ContentAlignment.MiddleLeft
                ? 14
                : Math.Max(10, (Width - contentWidth) / 2);

            if (glyphSize > 0)
            {
                Glyphs.Draw(g, _glyph,
                    new Rectangle(startX, (Height - glyphSize) / 2, glyphSize, glyphSize), text, 1.7f);
            }

            using (SolidBrush tb = new SolidBrush(text))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.LineAlignment = StringAlignment.Center;
                fmt.FormatFlags = StringFormatFlags.NoWrap;
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                int textLeft = startX + glyphSize + gap;
                g.DrawString(Text, Font, tb,
                    new Rectangle(textLeft, 0, Math.Max(4, Width - textLeft - 8), Height), fmt);
            }
        }

        private void ResolveColors(out Color fill, out Color border, out Color text)
        {
            switch (_variant)
            {
                case ButtonVariant.Primary:
                    fill = _pressed ? Theme.Navy800 : (_hovered ? Theme.Navy800 : Theme.Navy900);
                    border = fill;
                    text = Color.White;
                    break;

                case ButtonVariant.Brick:
                    fill = _hovered ? Theme.Brick700 : Theme.Brick600;
                    border = fill;
                    text = Color.White;
                    break;

                case ButtonVariant.Ghost:
                    fill = _hovered ? Theme.Slate100 : Color.Transparent;
                    border = Color.Transparent;
                    text = Theme.Navy700;
                    break;

                case ButtonVariant.Danger:
                    fill = _hovered ? Theme.Red100 : Theme.Red050;
                    border = Theme.Red100;
                    text = Theme.Red700;
                    break;

                default:
                    fill = _hovered ? Theme.Slate50 : Color.White;
                    border = _hovered ? Theme.Slate300 : Theme.Slate200;
                    text = Theme.Slate800;
                    break;
            }

            if (!_enabledLook)
            {
                fill = Theme.Slate50;
                border = Theme.Slate150;
                text = Theme.Slate400;
            }
        }
    }
}
