using System;
using System.Drawing;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>Factory helpers that keep repetitive control setup out of the views.</summary>
    internal static class Ui
    {
        /// <summary>Small uppercase mono eyebrow above a page title.</summary>
        public static Label Eyebrow(string text)
        {
            return new Label
            {
                AutoSize = true,
                Font = Theme.Eyebrow,
                ForeColor = Theme.Brick600,
                Text = Theme.Track(text.ToUpperInvariant())
            };
        }

        public static Label PageTitle(string text)
        {
            return new Label
            {
                AutoSize = true,
                Font = Theme.PageTitle,
                ForeColor = Theme.Navy950,
                Text = text
            };
        }

        public static Label SubTitle(string text)
        {
            return new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate600,
                Text = text
            };
        }

        /// <summary>Uppercase field label used above inputs and in card cells.</summary>
        public static Label FieldLabel(string text)
        {
            return new Label
            {
                AutoSize = true,
                Font = Theme.Label,
                ForeColor = Theme.Slate600,
                Text = Theme.Track(text.ToUpperInvariant())
            };
        }

        public static Label Body(string text, bool bold = false)
        {
            return new Label
            {
                AutoSize = true,
                Font = bold ? Theme.BodyBold : Theme.Body,
                ForeColor = Theme.Slate900,
                Text = text
            };
        }

        public static Label Muted(string text)
        {
            return new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate600,
                Text = text
            };
        }

        public static Label Help(string text)
        {
            return new Label
            {
                AutoSize = false,
                Font = Theme.Tiny,
                ForeColor = Theme.Slate400,
                Text = text
            };
        }

        /// <summary>A navy card header bar with uppercase title text.</summary>
        public static Card SectionBar(string title, int width, Color? fill = null)
        {
            Card bar = new Card
            {
                Fill = fill ?? Theme.Navy900,
                BorderWidth = 0,
                TopOnly = true,
                Radius = Theme.Radius,
                Height = Dpi.S(36),
                Width = width
            };

            Label label = new Label
            {
                AutoSize = true,
                Font = Theme.CardHeading,
                ForeColor = Color.White,
                Location = new Point(Dpi.S(17), Dpi.S(11)),
                Text = Theme.Track(title.ToUpperInvariant()),
                BackColor = Color.Transparent
            };
            bar.Controls.Add(label);

            return bar;
        }

        /// <summary>Section header used inside a form between item groups.</summary>
        public static Panel SubSectionBar(string title, int width)
        {
            Panel bar = new Panel
            {
                BackColor = Theme.Navy800,
                Height = Dpi.S(32),
                Width = width
            };

            Label label = new Label
            {
                AutoSize = true,
                Font = Theme.SmallBold,
                ForeColor = Color.White,
                Location = new Point(Dpi.S(17), Dpi.S(9)),
                Text = Theme.Track(title.ToUpperInvariant()),
                BackColor = Color.Transparent
            };
            bar.Controls.Add(label);

            return bar;
        }

        public static ComboBox Dropdown(int width)
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = Theme.Body,
                FlatStyle = FlatStyle.Flat,
                Width = width,
                Height = Dpi.S(26)
            };
        }

        public static CheckBox Check(string text)
        {
            return new CheckBox
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate800,
                Text = text,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat
            };
        }

        /// <summary>Horizontal hairline divider.</summary>
        public static Panel Divider(int width)
        {
            return new Panel
            {
                BackColor = Theme.Slate150,
                Height = Dpi.S(1),
                Width = width
            };
        }

        /// <summary>
        /// Wraps text to a pixel width and returns the required height, so views can
        /// lay out multi-line labels without guessing.
        /// </summary>
        public static int MeasureWrapped(string text, Font font, int width)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            using (Bitmap bmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                SizeF size = g.MeasureString(text, font, width);
                return (int)Math.Ceiling(size.Height);
            }
        }

        /// <summary>A wrapped multi-line label sized to fit its content.</summary>
        public static Label Wrapped(string text, Font font, Color color, int width)
        {
            Label label = new Label
            {
                AutoSize = false,
                Font = font,
                ForeColor = color,
                Text = text,
                Width = width,
                BackColor = Color.Transparent
            };
            label.Height = MeasureWrapped(text, font, width) + 2;
            return label;
        }
    }
}
