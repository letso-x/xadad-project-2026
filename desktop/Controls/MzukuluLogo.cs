using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>
    /// The Mzukulu Technologies logo: a rounded navy square with an overlapping
    /// red-and-white "M".
    ///
    /// If <c>Assets\logo.png</c> exists next to the executable it is used directly,
    /// so the exact supplied artwork can be dropped in without a code change. When
    /// the file is absent the mark is reconstructed with GDI+ so the app still shows
    /// correct branding out of the box.
    /// </summary>
    public class MzukuluLogo : Control
    {
        private static readonly Color Navy = Color.FromArgb(0x1E, 0x33, 0x55);
        private static readonly Color Red = Color.FromArgb(0xE0, 0x39, 0x3F);

        private static Image _cachedImage;
        private static bool _imageChecked;

        /// <summary>Optional caption drawn beneath the mark (e.g. "MZUKULU").</summary>
        private bool _showWordmark;

        public MzukuluLogo()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Dpi.S(56), Dpi.S(56));
        }

        /// <summary>Draws the "MZUKULU TECHNOLOGIES" wordmark under the mark.</summary>
        public bool ShowWordmark
        {
            get { return _showWordmark; }
            set { _showWordmark = value; Invalidate(); }
        }

        /// <summary>Loads the supplied PNG once, if present. Returns null otherwise.</summary>
        private static Image SuppliedImage()
        {
            if (_imageChecked) return _cachedImage;
            _imageChecked = true;

            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.png");

                if (File.Exists(path))
                {
                    // Copy into memory so the file handle is released immediately.
                    using (Image loaded = Image.FromFile(path))
                    {
                        _cachedImage = new Bitmap(loaded);
                    }
                }
            }
            catch (Exception)
            {
                _cachedImage = null;
            }

            return _cachedImage;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int markSize = Math.Min(Width, _showWordmark ? Height - Dpi.S(22) : Height);
            if (markSize < 2) return;

            Rectangle markRect = new Rectangle((Width - markSize) / 2, 0, markSize, markSize);

            Image supplied = SuppliedImage();
            if (supplied != null)
            {
                g.DrawImage(supplied, markRect);
            }
            else
            {
                DrawMark(g, markRect);
            }

            if (_showWordmark)
            {
                DrawWordmark(g, markRect.Bottom + Dpi.S(4));
            }
        }

        /// <summary>Reconstructs the mark: rounded navy tile with a layered red/white M.</summary>
        private static void DrawMark(Graphics g, Rectangle r)
        {
            int radius = Math.Max(2, r.Width / 5);

            using (GraphicsPath path = Card.Build(r, radius, false, false))
            using (SolidBrush navy = new SolidBrush(Navy))
            {
                g.FillPath(navy, path);
            }

            // Compose the M inside a padded inner box.
            int pad = (int)(r.Width * 0.24f);
            Rectangle inner = new Rectangle(r.X + pad, r.Y + pad,
                r.Width - pad * 2, r.Height - pad * 2);

            if (inner.Width < 4 || inner.Height < 4) return;

            float t = inner.Width * 0.30f;              // stroke thickness
            float midX = inner.X + inner.Width / 2f;
            float top = inner.Y;
            float bottom = inner.Bottom;

            // Red left half: a bold left leg plus the down-stroke into the centre.
            using (SolidBrush red = new SolidBrush(Red))
            using (GraphicsPath redPath = new GraphicsPath())
            {
                redPath.AddPolygon(new[]
                {
                    new PointF(inner.X, top),
                    new PointF(inner.X + t, top),
                    new PointF(midX + t * 0.15f, bottom - t * 0.2f),
                    new PointF(midX, bottom),
                    new PointF(inner.X, bottom)
                });
                g.FillPath(red, redPath);
            }

            // White right half: the right leg and the diagonal meeting the centre.
            using (SolidBrush white = new SolidBrush(Color.White))
            using (GraphicsPath whitePath = new GraphicsPath())
            {
                whitePath.AddPolygon(new[]
                {
                    new PointF(inner.Right, top),
                    new PointF(inner.Right - t, top),
                    new PointF(midX, inner.Y + inner.Height * 0.52f),
                    new PointF(midX + t * 0.5f, inner.Y + inner.Height * 0.52f),
                    new PointF(inner.Right - t, bottom),
                    new PointF(inner.Right, bottom)
                });
                g.FillPath(white, whitePath);
            }
        }

        private void DrawWordmark(Graphics g, int top)
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (Font nameFont = new Font("Segoe UI", 8.5F, FontStyle.Bold))
            using (SolidBrush navy = new SolidBrush(Navy))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                g.DrawString("MZUKULU", nameFont, navy,
                    new RectangleF(0, top, Width, Dpi.S(16)), fmt);
            }
        }
    }
}
