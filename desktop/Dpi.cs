using System;
using System.Drawing;
using System.Windows.Forms;

namespace MzuApplication
{
    /// <summary>
    /// DPI scaling for the hand-laid-out UI.
    ///
    /// The app declares per-monitor DPI awareness in its manifest, so Windows renders
    /// it at the monitor's real pixel density instead of drawing at 96 DPI and
    /// bitmap-stretching the result (which is what made text look blurry). Because
    /// the layout uses explicit pixel positions rather than a layout engine, those
    /// values have to be scaled here.
    /// </summary>
    internal static class Dpi
    {
        private static float _scale = 1f;
        private static bool _resolved;

        /// <summary>Current scale factor: 1.0 at 96 DPI, 1.5 at 144 DPI, and so on.</summary>
        public static float Scale
        {
            get
            {
                if (!_resolved) Resolve();
                return _scale;
            }
        }

        /// <summary>
        /// Reads the device DPI once, from a real device context so the value reflects
        /// the display rather than a cached default.
        /// </summary>
        private static void Resolve()
        {
            try
            {
                using (Bitmap bmp = new Bitmap(1, 1))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    _scale = g.DpiX / 96f;
                }
            }
            catch (Exception)
            {
                _scale = 1f;
            }

            if (_scale < 1f) _scale = 1f;       // never shrink below the design size
            if (_scale > 4f) _scale = 4f;       // guard against nonsense values

            _resolved = true;
        }

        /// <summary>
        /// Called once at startup, after the DPI context is established, so the scale
        /// is captured before any control is created.
        /// </summary>
        public static void Initialise()
        {
            _resolved = false;
            Resolve();
        }

        /// <summary>Scales a pixel measurement.</summary>
        public static int S(int value)
        {
            return (int)Math.Round(value * Scale);
        }

        /// <summary>Scales a float measurement.</summary>
        public static float S(float value)
        {
            return value * Scale;
        }

        public static Size S(Size value)
        {
            return new Size(S(value.Width), S(value.Height));
        }

        public static Point S(Point value)
        {
            return new Point(S(value.X), S(value.Y));
        }

        public static Padding S(Padding value)
        {
            return new Padding(S(value.Left), S(value.Top), S(value.Right), S(value.Bottom));
        }

        /// <summary>
        /// Fonts are supplied in points, which GDI+ already scales by DPI, so point
        /// sizes are returned unchanged. This exists to make that decision explicit
        /// at the call sites rather than leaving it implicit.
        /// </summary>
        public static float FontPoints(float points)
        {
            return points;
        }
    }
}
