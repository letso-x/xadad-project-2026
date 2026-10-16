using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>
    /// A premium branded loading spinner: the Mzukulu "M" tile sits inside a rotating
    /// luminous arc while a soft glow pulses behind the mark, so it reads as the logo
    /// "charging up". Pure GDI+ at ~60fps, DPI-aware, with an optional caption.
    /// </summary>
    public class MzukuluSpinner : Control
    {
        private static readonly Color Navy = Color.FromArgb(0x0F, 0x23, 0x40);
        private static readonly Color NavyDeep = Color.FromArgb(0x0A, 0x15, 0x26);
        private static readonly Color Red = Color.FromArgb(0xE0, 0x39, 0x3F);
        private static readonly Color Glow = Color.FromArgb(0x3D, 0x6B, 0xA6);

        private readonly Timer _timer;
        private float _angle;      // rotating arc, degrees
        private float _phase;      // pulse phase, radians
        private string _caption = "Loading…";

        public MzukuluSpinner()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Dpi.S(160), Dpi.S(190));

            _timer = new Timer { Interval = 16 }; // ~60fps
            _timer.Tick += (s, e) =>
            {
                _angle = (_angle + 5f) % 360f;        // arc rotation speed
                _phase += 0.08f;                       // glow pulse speed
                if (_phase > Math.PI * 2) _phase -= (float)(Math.PI * 2);
                Invalidate();
            };
        }

        /// <summary>Text shown under the mark. Set null/empty to hide.</summary>
        public string Caption
        {
            get { return _caption; }
            set { _caption = value ?? string.Empty; Invalidate(); }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) _timer.Start(); else _timer.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Stop(); _timer.Dispose(); }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int tile = Dpi.S(84);
            int cx = Width / 2;
            int cy = Dpi.S(12) + tile / 2;

            // Pulse value 0..1 (eased sine) drives the glow intensity and mark brightness.
            float pulse = (float)((Math.Sin(_phase) + 1.0) / 2.0);

            DrawGlow(g, cx, cy, tile, pulse);
            DrawRotatingArc(g, cx, cy, tile, pulse);
            DrawTile(g, cx, cy, tile, pulse);
            DrawCaption(g, cy + tile / 2);
        }

        /// <summary>Soft radial halo behind the mark that breathes with the pulse.</summary>
        private static void DrawGlow(Graphics g, int cx, int cy, int tile, float pulse)
        {
            int radius = (int)(tile * (0.95f + pulse * 0.35f));
            Rectangle r = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(r);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    int alpha = (int)(70 + pulse * 90); // 70..160
                    brush.CenterColor = Color.FromArgb(alpha, Glow);
                    brush.SurroundColors = new[] { Color.FromArgb(0, Glow) };
                    g.FillPath(brush, path);
                }
            }
        }

        /// <summary>A luminous gradient arc that orbits the mark.</summary>
        private void DrawRotatingArc(Graphics g, int cx, int cy, int tile, float pulse)
        {
            int ring = (int)(tile * 0.72f);
            Rectangle r = new Rectangle(cx - ring, cy - ring, ring * 2, ring * 2);
            float thickness = Math.Max(2f, tile * 0.055f);

            // Faint full ring the arc runs along.
            using (Pen track = new Pen(Color.FromArgb(40, Glow), thickness))
            {
                track.StartCap = track.EndCap = LineCap.Round;
                g.DrawEllipse(track, r);
            }

            // Bright sweeping arc. A couple of trailing arcs give a comet-tail glow.
            for (int i = 0; i < 3; i++)
            {
                int alpha = (int)((200 - i * 60) * (0.6f + pulse * 0.4f));
                if (alpha < 0) alpha = 0;
                using (Pen pen = new Pen(Color.FromArgb(alpha, Lerp(Glow, Color.White, 0.25f * (3 - i))), thickness))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, r, _angle - i * 16f, 70f - i * 14f);
                }
            }
        }

        /// <summary>The rounded navy tile with the layered red/white "M", lit by the pulse.</summary>
        private void DrawTile(Graphics g, int cx, int cy, int tile, float pulse)
        {
            Rectangle r = new Rectangle(cx - tile / 2, cy - tile / 2, tile, tile);
            int radius = Math.Max(3, tile / 5);

            // Navy tile with a subtle vertical gradient for depth.
            using (GraphicsPath path = RoundRect(r, radius))
            using (LinearGradientBrush bg = new LinearGradientBrush(r, NavyDeep, Navy, 90f))
            {
                g.FillPath(bg, path);

                // Inner top sheen that brightens on the pulse — the "illuminate".
                Rectangle sheen = new Rectangle(r.X, r.Y, r.Width, r.Height / 2);
                using (GraphicsPath sp = RoundRect(sheen, radius))
                using (LinearGradientBrush sb = new LinearGradientBrush(sheen,
                    Color.FromArgb((int)(40 + pulse * 70), Color.White),
                    Color.FromArgb(0, Color.White), 90f))
                {
                    var clip = g.Clip;
                    g.SetClip(path);
                    g.FillRectangle(sb, sheen);
                    g.Clip = clip;
                }
            }

            // Compose the M inside a padded inner box.
            int pad = (int)(tile * 0.24f);
            Rectangle inner = new Rectangle(r.X + pad, r.Y + pad, r.Width - pad * 2, r.Height - pad * 2);
            if (inner.Width < 4) return;

            float t = inner.Width * 0.30f;
            float midX = inner.X + inner.Width / 2f;
            float top = inner.Y;
            float bottom = inner.Bottom;

            // Red brightens slightly with the pulse so the mark "glows".
            Color litRed = Lerp(Red, Color.White, pulse * 0.18f);
            using (SolidBrush red = new SolidBrush(litRed))
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

        private void DrawCaption(Graphics g, int top)
        {
            if (string.IsNullOrEmpty(_caption)) return;

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (Font f = new Font("Segoe UI", 9.5F, FontStyle.Regular))
            using (SolidBrush b = new SolidBrush(ForeColor.A == 0 ? Theme.Slate600 : ForeColor))
            using (StringFormat fmt = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString(_caption, f, b,
                    new RectangleF(0, top + Dpi.S(14), Width, Dpi.S(20)), fmt);
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static Color Lerp(Color a, Color b, float t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}
