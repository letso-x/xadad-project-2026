using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MzuApplication.Models;

namespace MzuApplication.Controls
{
    /// <summary>
    /// One entry in the Recent QC Activity timeline: status dot, document title, a meta
    /// line carrying project / record chip / updated date, and a status pill.
    /// </summary>
    public class ActivityRow : Control
    {
        private bool _hovered;

        public ActivityRow()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Dpi.S(60);
            Cursor = Cursors.Hand;
        }

        public string Title { get; set; }
        public string Project { get; set; }
        public string RecordNumber { get; set; }
        public string Updated { get; set; }
        public RecordStatus Status { get; set; }
        public bool ShowDivider { get; set; }

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
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Paint in design units; the scope maps them to device pixels.
            using (PaintScope scope = new PaintScope(g, Width, Height))
            {
                PaintRow(g, scope.W, scope.H);
            }
        }

        private void PaintRow(Graphics g, int Width, int Height)
        {
            if (_hovered)
            {
                using (SolidBrush hb = new SolidBrush(Theme.Slate50))
                {
                    g.FillRectangle(hb, 0, 0, Width, Height);
                }
            }

            // Leading timeline dot.
            using (SolidBrush dot = new SolidBrush(Format.StatusDot(Status)))
            {
                g.FillEllipse(dot, 17, 19, 8, 8);
            }

            // Status pill, right aligned.
            int pillWidth = 0;
            string statusText = Format.StatusText(Status);

            using (Font pf = Theme.TinyBold)
            {
                Glyph glyph = Status == RecordStatus.InProgress ? Glyph.Clock
                            : Status == RecordStatus.CompleteAccepted ? Glyph.CheckCircle
                            : Glyph.Alert;

                SizeF size = g.MeasureString(statusText, pf);
                pillWidth = (int)Math.Ceiling(size.Width) + 36;
                int pillHeight = 24;

                Rectangle pill = new Rectangle(Width - pillWidth - 16,
                    (Height - pillHeight) / 2, pillWidth, pillHeight);

                using (GraphicsPath path = Card.Build(pill, pillHeight / 2, false, false))
                using (SolidBrush pb = new SolidBrush(Format.StatusBack(Status)))
                {
                    g.FillPath(pb, path);
                }

                Color fore = Format.StatusFore(Status);
                Glyphs.Draw(g, glyph,
                    new Rectangle(pill.X + 10, pill.Y + (pillHeight - 12) / 2, 12, 12), fore, 1.7f);

                using (SolidBrush tb = new SolidBrush(fore))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.LineAlignment = StringAlignment.Center;
                    fmt.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(statusText, pf, tb,
                        new Rectangle(pill.X + 26, pill.Y, pill.Width - 30, pillHeight), fmt);
                }
            }

            int contentRight = Width - pillWidth - 26;

            // Title.
            using (Font tf = Theme.SmallBold)
            using (SolidBrush tb = new SolidBrush(Theme.Slate900))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.FormatFlags = StringFormatFlags.NoWrap;
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(Title, tf, tb,
                    new RectangleF(34, 13, Math.Max(20, contentRight - 34), 18), fmt);
            }

            // Meta line: project · [record chip] · updated date.
            using (Font mf = Theme.Tiny)
            using (SolidBrush mb = new SolidBrush(Theme.Slate500))
            {
                float x = 34;
                const float metaY = 33;
                float limit = contentRight;

                if (!string.IsNullOrEmpty(Project))
                {
                    // Give the project name at most half the available run so the chip
                    // and date always stay visible.
                    float allowance = Math.Max(60, (limit - x) * 0.5f);
                    using (StringFormat fmt = new StringFormat())
                    {
                        fmt.FormatFlags = StringFormatFlags.NoWrap;
                        fmt.Trimming = StringTrimming.EllipsisCharacter;
                        g.DrawString(Project, mf, mb, new RectangleF(x, metaY, allowance, 16), fmt);
                    }

                    float measured = g.MeasureString(Project, mf).Width;
                    x += Math.Min(measured, allowance) + 2;

                    g.DrawString("·", mf, mb, x, metaY);
                    x += 8;
                }

                if (!string.IsNullOrEmpty(RecordNumber))
                {
                    using (Font cf = Theme.MonoTiny)
                    {
                        SizeF chipText = g.MeasureString(RecordNumber, cf);
                        Rectangle chip = new Rectangle((int)x, (int)metaY - 1,
                            (int)Math.Ceiling(chipText.Width) + 12, 17);

                        using (GraphicsPath path = Card.Build(chip, 5, false, false))
                        using (SolidBrush cb = new SolidBrush(Theme.Slate100))
                        using (Pen cp = new Pen(Theme.Slate150))
                        {
                            g.FillPath(cb, path);
                            g.DrawPath(cp, path);
                        }

                        using (SolidBrush ctb = new SolidBrush(Theme.Navy900))
                        using (StringFormat fmt = new StringFormat())
                        {
                            fmt.Alignment = StringAlignment.Center;
                            fmt.LineAlignment = StringAlignment.Center;
                            g.DrawString(RecordNumber, cf, ctb, chip, fmt);
                        }

                        x += chip.Width + 5;
                    }

                    g.DrawString("·", mf, mb, x, metaY);
                    x += 8;
                }

                if (!string.IsNullOrEmpty(Updated) && x < limit - 20)
                {
                    using (StringFormat fmt = new StringFormat())
                    {
                        fmt.FormatFlags = StringFormatFlags.NoWrap;
                        fmt.Trimming = StringTrimming.EllipsisCharacter;
                        g.DrawString(Updated, mf, mb, new RectangleF(x, metaY, limit - x, 16), fmt);
                    }
                }
            }

            if (ShowDivider)
            {
                using (Pen p = new Pen(Theme.Slate100))
                {
                    g.DrawLine(p, 17, Height - 1, Width - 17, Height - 1);
                }
            }
        }
    }
}
