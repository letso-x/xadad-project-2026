using System;
using System.Drawing;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>
    /// Builds the summary tile used on the dashboard, NCR register and reports screen.
    /// Extracted so all three stay visually identical.
    /// </summary>
    internal static class StatTile
    {
        public static Card Build(Glyph glyph, string value, string label,
                                 bool warn, int width, int height, string caption = null)
        {
            Card tile = new Card
            {
                Width = width,
                Height = height,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card chip = new Card
            {
                Location = new Point(Dpi.S(18), Dpi.S(16)),
                Size = new Size(Dpi.S(32), Dpi.S(32)),
                Radius = Theme.RadiusSm,
                Fill = warn ? Theme.Red050 : Theme.Slate50,
                BorderTint = warn ? Theme.Red100 : Theme.Slate150
            };

            Color glyphColor = warn ? Theme.Red700 : Theme.Navy700;
            chip.Paint += (s, e) =>
            {
                int inset = Dpi.S(8);
                int size = Dpi.S(16);
                Glyphs.Draw(e.Graphics, glyph,
                    new Rectangle(inset, inset, size, size), glyphColor, Dpi.S(1.7f));
            };
            tile.Controls.Add(chip);

            Label number = new Label
            {
                AutoSize = true,
                Font = Theme.MonoNumber,
                ForeColor = warn ? Theme.Red700 : Theme.Navy950,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(16), Dpi.S(50)),
                Text = value
            };
            tile.Controls.Add(number);

            Label caption1 = new Label
            {
                AutoSize = false,
                Font = Theme.Small,
                ForeColor = Theme.Slate600,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(18), number.Bottom),
                Width = width - Dpi.S(28),
                Height = Dpi.S(16),
                Text = label
            };
            tile.Controls.Add(caption1);

            if (!string.IsNullOrEmpty(caption))
            {
                Label sub = new Label
                {
                    AutoSize = false,
                    Font = Theme.Tiny,
                    ForeColor = Theme.Slate400,
                    BackColor = Color.Transparent,
                    Location = new Point(Dpi.S(18), caption1.Bottom + Dpi.S(1)),
                    Width = width - Dpi.S(28),
                    Height = Dpi.S(15),
                    Text = caption
                };
                tile.Controls.Add(sub);
            }

            return tile;
        }
    }
}
