using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>A cell value plus optional per-cell rendering hints.</summary>
    public class TableCell
    {
        public string Text { get; set; }

        /// <summary>Secondary line rendered beneath the main text in a muted tone.</summary>
        public string SubText { get; set; }

        public bool Bold { get; set; }
        public bool Muted { get; set; }
        public bool Mono { get; set; }

        /// <summary>Render inside a QC-number chip.</summary>
        public bool Chip { get; set; }

        /// <summary>Render as a status pill using these colours.</summary>
        public Color? PillBack { get; set; }
        public Color? PillFore { get; set; }
        public Glyph PillGlyph { get; set; }

        public TableCell(string text)
        {
            Text = text ?? string.Empty;
            PillGlyph = Glyph.None;
        }
    }

    public class TableRow
    {
        public List<TableCell> Cells { get; set; }

        /// <summary>Opaque payload handed back on row click.</summary>
        public object Tag { get; set; }

        /// <summary>Lowercase haystack used by the filter box.</summary>
        public string FilterText { get; set; }

        public TableRow()
        {
            Cells = new List<TableCell>();
        }
    }

    /// <summary>
    /// A lightweight owner-drawn table. Chosen over DataGridView because the design
    /// needs multi-line cells, chips and pills inside cells, which DataGridView makes
    /// awkward without heavy custom painting anyway.
    /// </summary>
    public class DataTable : Panel
    {
        private readonly List<string> _headers = new List<string>();
        private readonly List<float> _weights = new List<float>();
        private readonly List<TableRow> _rows = new List<TableRow>();
        private readonly List<TableRow> _visible = new List<TableRow>();

        private const int HeaderHeight = 34;
        private const int RowHeight = 46;

        private int _hoverIndex = -1;

        public DataTable()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Cursor = Cursors.Default;
        }

        /// <summary>Raised with the clicked row's <see cref="TableRow.Tag"/>.</summary>
        public event EventHandler<object> RowActivated;

        /// <summary>Message shown when there are no rows.</summary>
        public string EmptyMessage { get; set; } = "Nothing to show yet.";

        public void DefineColumns(params object[] headerAndWeight)
        {
            _headers.Clear();
            _weights.Clear();

            for (int i = 0; i + 1 < headerAndWeight.Length; i += 2)
            {
                _headers.Add(Convert.ToString(headerAndWeight[i]));
                _weights.Add(Convert.ToSingle(headerAndWeight[i + 1]));
            }
        }

        public void SetRows(IEnumerable<TableRow> rows)
        {
            _rows.Clear();
            if (rows != null) _rows.AddRange(rows);
            ApplyFilter(string.Empty);
        }

        /// <summary>Shows only rows whose FilterText contains <paramref name="term"/>.</summary>
        public void ApplyFilter(string term)
        {
            string needle = (term ?? string.Empty).Trim().ToLowerInvariant();

            _visible.Clear();
            foreach (TableRow row in _rows)
            {
                if (needle.Length == 0
                    || (row.FilterText != null && row.FilterText.Contains(needle)))
                {
                    _visible.Add(row);
                }
            }

            Height = HeaderHeight + Math.Max(_visible.Count, 1) * RowHeight + 2;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int index = (e.Y - HeaderHeight) / RowHeight;
            if (e.Y < HeaderHeight || index < 0 || index >= _visible.Count) index = -1;

            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (_hoverIndex < 0 || _hoverIndex >= _visible.Count) return;

            EventHandler<object> handler = RowActivated;
            if (handler != null) handler(this, _visible[_hoverIndex].Tag);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int[] widths = ComputeWidths();

            // Header band.
            using (SolidBrush hb = new SolidBrush(Theme.Slate50))
            {
                g.FillRectangle(hb, 0, 0, Width, HeaderHeight);
            }
            using (Pen p = new Pen(Theme.Slate150))
            {
                g.DrawLine(p, 0, HeaderHeight - 1, Width, HeaderHeight - 1);
            }

            using (Font hf = Theme.Label)
            using (SolidBrush ht = new SolidBrush(Theme.Slate500))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.LineAlignment = StringAlignment.Center;
                fmt.FormatFlags = StringFormatFlags.NoWrap;
                fmt.Trimming = StringTrimming.EllipsisCharacter;

                int x = 14;
                for (int i = 0; i < _headers.Count; i++)
                {
                    g.DrawString(Theme.Track(_headers[i].ToUpperInvariant()), hf, ht,
                        new Rectangle(x, 0, widths[i] - 12, HeaderHeight), fmt);
                    x += widths[i];
                }
            }

            if (_visible.Count == 0)
            {
                using (Font f = Theme.Small)
                using (SolidBrush b = new SolidBrush(Theme.Slate500))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.Alignment = StringAlignment.Center;
                    fmt.LineAlignment = StringAlignment.Center;
                    g.DrawString(EmptyMessage, f, b,
                        new Rectangle(0, HeaderHeight, Width, RowHeight), fmt);
                }
                return;
            }

            for (int rowIndex = 0; rowIndex < _visible.Count; rowIndex++)
            {
                int top = HeaderHeight + rowIndex * RowHeight;

                if (rowIndex == _hoverIndex)
                {
                    using (SolidBrush hb = new SolidBrush(Theme.Slate50))
                    {
                        g.FillRectangle(hb, 0, top, Width, RowHeight);
                    }
                }

                if (rowIndex < _visible.Count - 1)
                {
                    using (Pen p = new Pen(Theme.Slate100))
                    {
                        g.DrawLine(p, 0, top + RowHeight - 1, Width, top + RowHeight - 1);
                    }
                }

                TableRow row = _visible[rowIndex];
                int x = 14;

                for (int c = 0; c < _headers.Count; c++)
                {
                    if (c >= row.Cells.Count) break;

                    DrawCell(g, row.Cells[c], new Rectangle(x, top, widths[c] - 12, RowHeight));
                    x += widths[c];
                }
            }
        }

        private void DrawCell(Graphics g, TableCell cell, Rectangle area)
        {
            // Status pill.
            if (cell.PillBack.HasValue)
            {
                using (Font pf = Theme.TinyBold)
                {
                    SizeF size = g.MeasureString(cell.Text, pf);
                    int glyphLead = cell.PillGlyph == Glyph.None ? 0 : 16;
                    int w = (int)Math.Ceiling(size.Width) + 18 + glyphLead;
                    int h = 22;
                    Rectangle pill = new Rectangle(area.X, area.Y + (area.Height - h) / 2,
                        Math.Min(w, area.Width), h);

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var path = Card.Build(pill, h / 2, false, false))
                    using (SolidBrush pb = new SolidBrush(cell.PillBack.Value))
                    {
                        g.FillPath(pb, path);
                    }

                    Color fore = cell.PillFore ?? Theme.Slate800;
                    int textLeft = pill.X + 9;

                    if (cell.PillGlyph != Glyph.None)
                    {
                        Glyphs.Draw(g, cell.PillGlyph,
                            new Rectangle(textLeft, pill.Y + (h - 12) / 2, 12, 12), fore, 1.7f);
                        textLeft += 16;
                    }

                    using (SolidBrush tb = new SolidBrush(fore))
                    using (StringFormat fmt = new StringFormat())
                    {
                        fmt.LineAlignment = StringAlignment.Center;
                        fmt.FormatFlags = StringFormatFlags.NoWrap;
                        g.DrawString(cell.Text, pf, tb,
                            new Rectangle(textLeft, pill.Y, pill.Right - textLeft - 4, h), fmt);
                    }
                }
                return;
            }

            // QC number chip.
            if (cell.Chip)
            {
                using (Font cf = Theme.MonoTiny)
                {
                    SizeF size = g.MeasureString(cell.Text, cf);
                    int w = (int)Math.Ceiling(size.Width) + 16;
                    int h = 20;
                    Rectangle chip = new Rectangle(area.X, area.Y + (area.Height - h) / 2,
                        Math.Min(w, area.Width), h);

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var path = Card.Build(chip, Theme.RadiusSm, false, false))
                    using (SolidBrush b = new SolidBrush(Theme.Slate100))
                    using (Pen p = new Pen(Theme.Slate150))
                    {
                        g.FillPath(b, path);
                        g.DrawPath(p, path);
                    }

                    using (SolidBrush tb = new SolidBrush(Theme.Navy900))
                    using (StringFormat fmt = new StringFormat())
                    {
                        fmt.Alignment = StringAlignment.Center;
                        fmt.LineAlignment = StringAlignment.Center;
                        g.DrawString(cell.Text, cf, tb, chip, fmt);
                    }
                }
                return;
            }

            bool hasSub = !string.IsNullOrEmpty(cell.SubText);

            Font font = cell.Mono ? Theme.MonoSmall
                      : cell.Bold ? Theme.BodyBold
                      : cell.Muted ? Theme.Small
                      : Theme.Body;
            Color color = cell.Muted ? Theme.Slate600 : Theme.Slate900;

            using (font)
            using (SolidBrush b = new SolidBrush(color))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.FormatFlags = StringFormatFlags.NoWrap;
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                fmt.LineAlignment = hasSub ? StringAlignment.Near : StringAlignment.Center;

                Rectangle textArea = hasSub
                    ? new Rectangle(area.X, area.Y + 8, area.Width, 18)
                    : area;

                g.DrawString(cell.Text, font, b, textArea, fmt);
            }

            if (hasSub)
            {
                using (Font sf = Theme.MonoTiny)
                using (SolidBrush sb = new SolidBrush(Theme.Slate500))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.FormatFlags = StringFormatFlags.NoWrap;
                    fmt.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(cell.SubText, sf, sb,
                        new Rectangle(area.X, area.Y + 25, area.Width, 16), fmt);
                }
            }
        }

        private int[] ComputeWidths()
        {
            int[] widths = new int[_headers.Count];
            if (_headers.Count == 0) return widths;

            float totalWeight = 0;
            foreach (float w in _weights) totalWeight += w;
            if (totalWeight <= 0) totalWeight = _headers.Count;

            int available = Width - 14;
            int used = 0;

            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = i == widths.Length - 1
                    ? available - used
                    : (int)(available * (_weights[i] / totalWeight));
                used += widths[i];
            }

            return widths;
        }
    }
}
