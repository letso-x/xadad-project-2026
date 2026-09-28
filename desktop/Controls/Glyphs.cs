using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MzuApplication.Controls
{
    /// <summary>Stroke icon set mirroring the SVG paths in the approved design.</summary>
    public enum Glyph
    {
        None,
        Grid,
        Building,
        Folder,
        Layers,
        Tool,
        Search,
        Users,
        ChevronRight,
        Check,
        CheckCircle,
        Alert,
        Clock,
        Plus,
        Cross,
        Clipboard,
        Cable,
        BarChart,
        Logout,
        Pencil,
        Menu,
        ChevronLeft,
        Download,
        Shield,
        Bell,
        Filter,
        Gear
    }

    /// <summary>
    /// Draws the icon set with GDI+ so the project needs no image assets and the icons
    /// stay crisp at any scale.
    /// </summary>
    internal static class Glyphs
    {
        /// <summary>Draws <paramref name="glyph"/> to fit the given square-ish bounds.</summary>
        public static void Draw(Graphics g, Glyph glyph, Rectangle bounds, Color color, float strokeWidth = 1.6f)
        {
            if (glyph == Glyph.None || bounds.Width <= 0 || bounds.Height <= 0) return;

            SmoothingMode prevSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // All paths below are authored on a 24x24 grid then scaled to bounds.
            GraphicsState state = g.Save();
            g.TranslateTransform(bounds.X, bounds.Y);
            float scale = Math.Min(bounds.Width / 24f, bounds.Height / 24f);
            g.ScaleTransform(scale, scale);

            using (Pen pen = new Pen(color, strokeWidth / scale))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                switch (glyph)
                {
                    case Glyph.Grid: Grid(g, pen); break;
                    case Glyph.Building: Building(g, pen); break;
                    case Glyph.Folder: Folder(g, pen); break;
                    case Glyph.Layers: Layers(g, pen); break;
                    case Glyph.Tool: Tool(g, pen); break;
                    case Glyph.Search: Search(g, pen); break;
                    case Glyph.Users: Users(g, pen); break;
                    case Glyph.ChevronRight: Polyline(g, pen, 9, 18, 15, 12, 9, 6); break;
                    case Glyph.Check: Polyline(g, pen, 20, 6, 9, 17, 4, 12); break;
                    case Glyph.CheckCircle: CheckCircle(g, pen); break;
                    case Glyph.Alert: Alert(g, pen); break;
                    case Glyph.Clock: Clock(g, pen); break;
                    case Glyph.Plus: Plus(g, pen); break;
                    case Glyph.Cross: Cross(g, pen); break;
                    case Glyph.Clipboard: Clipboard(g, pen); break;
                    case Glyph.Cable: Cable(g, pen); break;
                    case Glyph.BarChart: BarChart(g, pen); break;
                    case Glyph.Logout: Logout(g, pen); break;
                    case Glyph.Pencil: Pencil(g, pen); break;
                    case Glyph.Menu: Menu(g, pen); break;
                    case Glyph.ChevronLeft: Polyline(g, pen, 15, 18, 9, 12, 15, 6); break;
                    case Glyph.Download: Download(g, pen); break;
                    case Glyph.Shield: Shield(g, pen); break;
                    case Glyph.Bell: Bell(g, pen); break;
                    case Glyph.Filter: Filter(g, pen); break;
                    case Glyph.Gear: Gear(g, pen); break;
                }
            }

            g.Restore(state);
            g.SmoothingMode = prevSmoothing;
        }

        private static void Polyline(Graphics g, Pen pen, params float[] pts)
        {
            PointF[] points = new PointF[pts.Length / 2];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new PointF(pts[i * 2], pts[i * 2 + 1]);
            }
            g.DrawLines(pen, points);
        }

        private static void Grid(Graphics g, Pen pen)
        {
            g.DrawRectangle(pen, 3, 3, 7, 7);
            g.DrawRectangle(pen, 14, 3, 7, 7);
            g.DrawRectangle(pen, 14, 14, 7, 7);
            g.DrawRectangle(pen, 3, 14, 7, 7);
        }

        private static void Building(Graphics g, Pen pen)
        {
            Polyline(g, pen, 4, 21, 4, 3, 12, 3, 12, 21);
            Polyline(g, pen, 15, 21, 15, 8, 21, 8, 21, 21);
            g.DrawLine(pen, 3, 21, 21, 21);
            for (int row = 0; row < 3; row++)
            {
                float y = 7 + row * 4;
                g.DrawLine(pen, 7, y, 7.6f, y);
                g.DrawLine(pen, 10, y, 10.6f, y);
            }
        }

        private static void Folder(Graphics g, Pen pen)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLine(3, 18, 3, 6);
                path.AddLine(3, 6, 9, 6);
                path.AddLine(9, 6, 11, 8);
                path.AddLine(11, 8, 21, 8);
                path.AddLine(21, 8, 21, 18);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }
        }

        private static void Layers(Graphics g, Pen pen)
        {
            Polyline(g, pen, 12, 3, 2, 8, 12, 13, 22, 8, 12, 3);
            Polyline(g, pen, 2, 13, 12, 18, 22, 13);
            Polyline(g, pen, 2, 18, 12, 23, 22, 18);
        }

        private static void Tool(Graphics g, Pen pen)
        {
            Polyline(g, pen, 14.7f, 6.3f, 15, 12, 18, 9, 17.7f, 3.3f);
            Polyline(g, pen, 4, 17, 9.3f, 11.7f, 12, 14.4f, 7, 20);
            g.DrawLine(pen, 4, 17, 7, 20);
        }

        private static void Search(Graphics g, Pen pen)
        {
            g.DrawEllipse(pen, 4, 4, 14, 14);
            g.DrawLine(pen, 21, 21, 16.7f, 16.7f);
        }

        private static void Users(Graphics g, Pen pen)
        {
            Polyline(g, pen, 1, 21, 1, 19);
            g.DrawArc(pen, 1, 15, 16, 8, 180, 180);
            g.DrawEllipse(pen, 5, 3, 8, 8);
            g.DrawArc(pen, 15, 15.5f, 8, 7, 200, 140);
            g.DrawArc(pen, 15, 3, 7, 8, 300, 120);
        }

        private static void CheckCircle(Graphics g, Pen pen)
        {
            g.DrawEllipse(pen, 3, 3, 18, 18);
            Polyline(g, pen, 8.5f, 12.3f, 10.8f, 14.6f, 15.5f, 9.6f);
        }

        private static void Alert(Graphics g, Pen pen)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLine(12, 2, 23, 21);
                path.AddLine(23, 21, 1, 21);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }
            g.DrawLine(pen, 12, 9, 12, 14);
            g.DrawLine(pen, 12, 17.4f, 12, 17.6f);
        }

        private static void Clock(Graphics g, Pen pen)
        {
            g.DrawEllipse(pen, 3, 3, 18, 18);
            Polyline(g, pen, 12, 7, 12, 12, 15, 14);
        }

        private static void Plus(Graphics g, Pen pen)
        {
            g.DrawLine(pen, 12, 5, 12, 19);
            g.DrawLine(pen, 5, 12, 19, 12);
        }

        private static void Cross(Graphics g, Pen pen)
        {
            g.DrawLine(pen, 18, 6, 6, 18);
            g.DrawLine(pen, 6, 6, 18, 18);
        }

        private static void Clipboard(Graphics g, Pen pen)
        {
            g.DrawRectangle(pen, 6, 6, 12, 15);
            g.DrawRectangle(pen, 9, 3, 6, 3);
            g.DrawLine(pen, 9, 11, 15, 11);
            g.DrawLine(pen, 9, 15, 15, 15);
        }

        private static void Cable(Graphics g, Pen pen)
        {
            Polyline(g, pen, 4, 4, 4, 9);
            g.DrawArc(pen, 4, 5, 8, 8, 180, -90);
            g.DrawLine(pen, 8, 13, 16, 13);
            g.DrawArc(pen, 12, 13, 8, 8, 270, 90);
            g.DrawLine(pen, 20, 17, 20, 20);
            g.DrawLine(pen, 2, 4, 6, 4);
            g.DrawLine(pen, 18, 20, 22, 20);
        }

        private static void BarChart(Graphics g, Pen pen)
        {
            g.DrawLine(pen, 4, 20, 4, 10);
            g.DrawLine(pen, 12, 20, 12, 4);
            g.DrawLine(pen, 20, 20, 20, 13);
        }

        private static void Logout(Graphics g, Pen pen)
        {
            Polyline(g, pen, 9, 21, 5, 21, 3, 19, 3, 5, 5, 3, 9, 3);
            Polyline(g, pen, 16, 17, 21, 12, 16, 7);
            g.DrawLine(pen, 21, 12, 9, 12);
        }

        private static void Pencil(Graphics g, Pen pen)
        {
            Polyline(g, pen, 4, 20, 4, 16, 16, 4, 20, 8, 8, 20, 4, 20);
        }

        private static void Menu(Graphics g, Pen pen)
        {
            g.DrawLine(pen, 3, 6, 21, 6);
            g.DrawLine(pen, 3, 12, 21, 12);
            g.DrawLine(pen, 3, 18, 21, 18);
        }

        private static void Download(Graphics g, Pen pen)
        {
            g.DrawLine(pen, 12, 3, 12, 15);
            Polyline(g, pen, 7, 10, 12, 15, 17, 10);
            Polyline(g, pen, 4, 18, 4, 21, 20, 21, 20, 18);
        }

        private static void Shield(Graphics g, Pen pen)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLine(12, 2, 20, 5);
                path.AddBezier(20, 5, 20, 14, 17, 19, 12, 22);
                path.AddBezier(12, 22, 7, 19, 4, 14, 4, 5);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }
            Polyline(g, pen, 9, 11.5f, 11.2f, 14, 15.5f, 9);
        }

        private static void Bell(Graphics g, Pen pen)
        {
            Polyline(g, pen, 6, 16, 6, 10);
            g.DrawArc(pen, 6, 3, 12, 12, 180, 180);
            Polyline(g, pen, 18, 10, 18, 16);
            g.DrawLine(pen, 4, 17, 20, 17);
            g.DrawArc(pen, 10, 17, 4, 4, 0, 180);
        }

        private static void Filter(Graphics g, Pen pen)
        {
            Polyline(g, pen, 3, 5, 21, 5, 14, 13, 14, 20, 10, 18, 10, 13, 3, 5);
        }

        private static void Gear(Graphics g, Pen pen)
        {
            g.DrawEllipse(pen, 9, 9, 6, 6);
            g.DrawEllipse(pen, 4, 4, 16, 16);
            for (int i = 0; i < 4; i++)
            {
                double angle = Math.PI / 2 * i;
                float x1 = 12f + (float)(Math.Cos(angle) * 8.5);
                float y1 = 12f + (float)(Math.Sin(angle) * 8.5);
                float x2 = 12f + (float)(Math.Cos(angle) * 11);
                float y2 = 12f + (float)(Math.Sin(angle) * 11);
                g.DrawLine(pen, x1, y1, x2, y2);
            }
        }
    }
}
