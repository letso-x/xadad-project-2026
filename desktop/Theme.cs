using System.Drawing;

namespace MzuApplication
{
    /// <summary>
    /// Central palette, metrics and fonts. Values mirror the CSS custom properties in
    /// the approved design so every screen stays visually consistent.
    /// </summary>
    internal static class Theme
    {
        // ---- Navy ----------------------------------------------------------
        public static readonly Color Navy950 = Color.FromArgb(0x0A, 0x15, 0x26);
        public static readonly Color Navy900 = Color.FromArgb(0x0F, 0x23, 0x40);
        public static readonly Color Navy800 = Color.FromArgb(0x15, 0x32, 0x52);
        public static readonly Color Navy700 = Color.FromArgb(0x1C, 0x40, 0x66);
        public static readonly Color Navy600 = Color.FromArgb(0x2A, 0x54, 0x8A);
        public static readonly Color Navy500 = Color.FromArgb(0x3D, 0x6B, 0xA6);

        // ---- Slate ---------------------------------------------------------
        public static readonly Color Slate25 = Color.FromArgb(0xFA, 0xFB, 0xFC);
        public static readonly Color Slate50 = Color.FromArgb(0xF4, 0xF6, 0xF8);
        public static readonly Color Slate100 = Color.FromArgb(0xEB, 0xEE, 0xF1);
        public static readonly Color Slate150 = Color.FromArgb(0xE1, 0xE5, 0xEA);
        public static readonly Color Slate200 = Color.FromArgb(0xD2, 0xD9, 0xE0);
        public static readonly Color Slate300 = Color.FromArgb(0xB8, 0xC1, 0xCA);
        public static readonly Color Slate400 = Color.FromArgb(0x8C, 0x99, 0xA6);
        public static readonly Color Slate500 = Color.FromArgb(0x6B, 0x78, 0x87);
        public static readonly Color Slate600 = Color.FromArgb(0x54, 0x62, 0x6F);
        public static readonly Color Slate700 = Color.FromArgb(0x3D, 0x48, 0x54);
        public static readonly Color Slate800 = Color.FromArgb(0x28, 0x32, 0x3B);
        public static readonly Color Slate900 = Color.FromArgb(0x17, 0x1E, 0x24);

        // ---- Brick (brand accent) ------------------------------------------
        public static readonly Color Brick700 = Color.FromArgb(0x8F, 0x32, 0x26);
        public static readonly Color Brick600 = Color.FromArgb(0xA8, 0x3F, 0x31);
        public static readonly Color Brick500 = Color.FromArgb(0xBE, 0x58, 0x47);
        public static readonly Color Brick100 = Color.FromArgb(0xF4, 0xE0, 0xDB);
        public static readonly Color Brick050 = Color.FromArgb(0xFB, 0xF1, 0xEE);

        // ---- Amber (in progress) -------------------------------------------
        public static readonly Color Amber700 = Color.FromArgb(0x8A, 0x5A, 0x12);
        public static readonly Color Amber600 = Color.FromArgb(0xAD, 0x7A, 0x20);
        public static readonly Color Amber100 = Color.FromArgb(0xFA, 0xEB, 0xD0);
        public static readonly Color Amber050 = Color.FromArgb(0xFD, 0xF6, 0xEA);

        // ---- Green (accepted) ----------------------------------------------
        public static readonly Color Green700 = Color.FromArgb(0x1F, 0x6E, 0x45);
        public static readonly Color Green600 = Color.FromArgb(0x2C, 0x8A, 0x5A);
        public static readonly Color Green100 = Color.FromArgb(0xDB, 0xEF, 0xE1);
        public static readonly Color Green050 = Color.FromArgb(0xEF, 0xF8, 0xF2);

        // ---- Red (rejected / NCR) ------------------------------------------
        public static readonly Color Red700 = Color.FromArgb(0xA2, 0x31, 0x27);
        public static readonly Color Red600 = Color.FromArgb(0xC4, 0x45, 0x3A);
        public static readonly Color Red100 = Color.FromArgb(0xF6, 0xDB, 0xD8);
        public static readonly Color Red050 = Color.FromArgb(0xFC, 0xEE, 0xEC);

        // ---- Metrics -------------------------------------------------------
        // Design values are authored at 96 DPI and scaled to the display, so the
        // layout keeps its proportions instead of shrinking on high-DPI screens.
        public static int SidebarWidth { get { return Dpi.S(246); } }
        public static int TopHeaderHeight { get { return Dpi.S(60); } }
        public static int PagePadding { get { return Dpi.S(28); } }
        public static int RadiusSm { get { return Dpi.S(8); } }
        public static int Radius { get { return Dpi.S(12); } }

        // ---- Fonts ---------------------------------------------------------
        // Segoe UI stands in for IBM Plex Sans; Consolas for IBM Plex Mono.
        private const string Sans = "Segoe UI";
        private const string Mono = "Consolas";

        // Point sizes are tuned to the approved design's density: ~13px body and a
        // 22px page title at 96 DPI. They are deliberately restrained for an
        // enterprise tool. DPI awareness scales them up on high-DPI displays, and
        // the layout scales with them via Dpi.S(), so proportions hold at any scale.
        public static Font PageTitle { get { return new Font(Sans, 13.5F, FontStyle.Bold); } }
        public static Font SectionTitle { get { return new Font(Sans, 9.5F, FontStyle.Bold); } }
        public static Font CardHeading { get { return new Font(Sans, 8F, FontStyle.Bold); } }
        public static Font Body { get { return new Font(Sans, 8F, FontStyle.Regular); } }
        public static Font BodyBold { get { return new Font(Sans, 8F, FontStyle.Bold); } }
        public static Font Small { get { return new Font(Sans, 7.5F, FontStyle.Regular); } }
        public static Font SmallBold { get { return new Font(Sans, 7.5F, FontStyle.Bold); } }
        public static Font Tiny { get { return new Font(Sans, 7F, FontStyle.Regular); } }
        public static Font TinyBold { get { return new Font(Sans, 7F, FontStyle.Bold); } }
        public static Font Label { get { return new Font(Sans, 6.75F, FontStyle.Bold); } }
        public static Font Eyebrow { get { return new Font(Mono, 7F, FontStyle.Bold); } }
        public static Font MonoNumber { get { return new Font(Mono, 15F, FontStyle.Bold); } }
        public static Font MonoSmall { get { return new Font(Mono, 7.25F, FontStyle.Bold); } }
        public static Font MonoTiny { get { return new Font(Mono, 6.75F, FontStyle.Bold); } }
        public static Font NavItem { get { return new Font(Sans, 8.25F, FontStyle.Regular); } }
        public static Font NavItemActive { get { return new Font(Sans, 8.25F, FontStyle.Bold); } }

        /// <summary>Adds letter spacing to a string the way the CSS tracking does.</summary>
        public static string Track(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            char[] chars = value.ToCharArray();
            System.Text.StringBuilder sb = new System.Text.StringBuilder(chars.Length * 2);
            for (int i = 0; i < chars.Length; i++)
            {
                sb.Append(chars[i]);
                if (i < chars.Length - 1) sb.Append('\u2009');
            }
            return sb.ToString();
        }
    }
}
