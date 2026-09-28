using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>
    /// A bordered input that hosts a borderless TextBox, adds focus ring styling, an
    /// optional leading search glyph and placeholder text.
    /// </summary>
    public class TextInput : Control
    {
        private readonly TextBox _box;
        private bool _focused;
        private string _placeholder = string.Empty;
        private bool _showingPlaceholder;
        private Glyph _glyph = Glyph.None;
        private bool _rounded;

        public TextInput()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Dpi.S(34);

            _box = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Theme.Body,
                ForeColor = Theme.Slate900,
                BackColor = Color.White
            };
            _box.GotFocus += (s, e) => { _focused = true; ClearPlaceholder(); Invalidate(); };
            _box.LostFocus += (s, e) => { _focused = false; ApplyPlaceholderIfEmpty(); Invalidate(); };
            _box.TextChanged += (s, e) => OnValueChanged();

            Controls.Add(_box);
        }

        /// <summary>Raised when the user edits the value (not when placeholder text is set).</summary>
        public event EventHandler ValueChanged;

        /// <summary>Raised when Enter is pressed inside the field.</summary>
        public event EventHandler Submitted;

        public Glyph Glyph
        {
            get { return _glyph; }
            set { _glyph = value; LayoutBox(); Invalidate(); }
        }

        /// <summary>Fully rounded ends, used for the top-bar search field.</summary>
        public bool Rounded
        {
            get { return _rounded; }
            set { _rounded = value; Invalidate(); }
        }

        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value ?? string.Empty;
                ApplyPlaceholderIfEmpty();
            }
        }

        /// <summary>Multiline text area behaviour.</summary>
        public bool Multiline
        {
            get { return _box.Multiline; }
            set
            {
                _box.Multiline = value;
                _box.ScrollBars = value ? ScrollBars.Vertical : ScrollBars.None;
                LayoutBox();
            }
        }

        public bool ReadOnlyValue
        {
            get { return _box.ReadOnly; }
            set { _box.ReadOnly = value; Invalidate(); }
        }

        /// <summary>The real value, empty while the placeholder is displayed.</summary>
        public string Value
        {
            get { return _showingPlaceholder ? string.Empty : _box.Text; }
            set
            {
                _showingPlaceholder = false;
                _box.ForeColor = Theme.Slate900;
                _box.Text = value ?? string.Empty;
                ApplyPlaceholderIfEmpty();
            }
        }

        public override Font Font
        {
            get { return base.Font; }
            set
            {
                base.Font = value;
                if (_box != null) { _box.Font = value; LayoutBox(); }
            }
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !_box.Multiline)
                {
                    EventHandler handler = Submitted;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            };
            LayoutBox();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutBox();
        }

        private void LayoutBox()
        {
            if (_box == null) return;

            int left = _glyph == Glyph.None ? 11 : 32;
            _box.Left = left;
            _box.Width = Math.Max(20, Width - left - 11);

            if (_box.Multiline)
            {
                _box.Top = Dpi.S(9);
                _box.Height = Math.Max(20, Height - 18);
            }
            else
            {
                _box.Top = Math.Max(2, (Height - _box.PreferredHeight) / 2 + 1);
                _box.Height = _box.PreferredHeight;
            }
        }

        private void ClearPlaceholder()
        {
            if (!_showingPlaceholder) return;
            _showingPlaceholder = false;
            _box.Text = string.Empty;
            _box.ForeColor = Theme.Slate900;
        }

        private void ApplyPlaceholderIfEmpty()
        {
            if (_box.Text.Length != 0 || _placeholder.Length == 0 || _focused) return;
            _showingPlaceholder = true;
            _box.Text = _placeholder;
            _box.ForeColor = Theme.Slate400;
        }

        private void OnValueChanged()
        {
            if (_showingPlaceholder) return;
            EventHandler handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Paint in 96 DPI design units; PaintScope maps them to device pixels
            // so text and icons render at full resolution instead of being stretched.
            using (PaintScope scope = new PaintScope(g, Width, Height))
            {
                PaintField(g, scope.W, scope.H);
            }
        }

        private void PaintField(Graphics g, int Width, int Height)
        {

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            int radius = _rounded ? Math.Max(1, r.Height / 2) : Theme.RadiusSm;
            Color fill = _box.ReadOnly ? Theme.Slate50 : (_focused || !_rounded ? Color.White : Theme.Slate50);
            Color border = _focused ? Theme.Navy500 : Theme.Slate200;

            _box.BackColor = fill;

            using (GraphicsPath path = Card.Build(r, radius, false, false))
            {
                if (_focused)
                {
                    // Soft focus ring, matching the CSS box-shadow.
                    using (Pen ring = new Pen(Color.FromArgb(36, 42, 84, 138), 3f))
                    {
                        g.DrawPath(ring, path);
                    }
                }

                using (SolidBrush b = new SolidBrush(fill))
                {
                    g.FillPath(b, path);
                }
                using (Pen pen = new Pen(border))
                {
                    g.DrawPath(pen, path);
                }
            }

            if (_glyph != Glyph.None)
            {
                Glyphs.Draw(g, _glyph, new Rectangle(10, (Height - 15) / 2, 15, 15), Theme.Slate400, 1.7f);
            }
        }
    }
}
