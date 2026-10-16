using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Forms;
using MzuApplication.Models;

namespace MzuApplication.Views
{
    /// <summary>
    /// Cross-entity search over clients, projects, forms, registers and templates.
    /// </summary>
    public class SearchView : ViewBase
    {
        private readonly string _initialQuery;
        private TextInput _input;

        public SearchView(MainForm shell, string initialQuery) : base(shell)
        {
            _initialQuery = initialQuery ?? string.Empty;
        }

        public override string RouteKey
        {
            get { return "search"; }
        }

        protected override void BuildContent()
        {
            AddPageHeader("Search", "Search", null);

            Card box = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Height = Dpi.S(96),
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            _input = new TextInput
            {
                Glyph = Glyph.Search,
                Placeholder = "Search clients, projects, QC document numbers, record numbers, status...",
                Location = new Point(Dpi.S(20), Dpi.S(20)),
                Width = InnerWidth - 40,
                Height = Dpi.S(40),
                Font = Theme.Body,
                Value = _initialQuery
            };
            _input.Submitted += (s, e) => Rebuild();
            box.Controls.Add(_input);

            Label help = Ui.Help("Try a client name, project name, document number (e.g. MZT-QC-301), "
                                 + "or a QC record number (e.g. QC-0001).");
            help.Location = new Point(20, _input.Bottom + 8);
            help.Width = InnerWidth - 40;
            help.Height = Dpi.S(16);
            box.Controls.Add(help);

            Controls.Add(box);
            Y = box.Bottom + 20;

            if (_initialQuery.Trim().Length > 0)
            {
                AddResults(_initialQuery.Trim().ToLowerInvariant());
            }
        }

        private void AddResults(string needle)
        {
            var results = new List<ResultEntry>();

            foreach (Client client in Repository.Db.Clients)
            {
                if (Contains(needle, client.Name, client.Contact))
                {
                    results.Add(new ResultEntry
                    {
                        Type = "Client",
                        Glyph = Glyph.Building,
                        Label = client.Name,
                        Detail = client.Contact ?? string.Empty,
                        Navigate = () => Shell.Navigate(new ClientsView(Shell))
                    });
                }
            }

            foreach (Project project in Repository.Db.Projects)
            {
                Client client = Repository.GetClient(project.ClientId);
                if (Contains(needle, project.Name, project.Number, project.Site,
                             client != null ? client.Name : null, project.Status))
                {
                    string projectId = project.Id;
                    results.Add(new ResultEntry
                    {
                        Type = "Project",
                        Glyph = Glyph.Folder,
                        Label = project.Name,
                        Detail = (client != null ? client.Name + " · " : string.Empty) + project.Number,
                        Navigate = () => Shell.Navigate(new ProjectDetailView(Shell, projectId))
                    });
                }
            }

            foreach (QcForm form in Repository.Db.Forms)
            {
                ChecklistTemplate template = Repository.GetTemplate(form.TemplateId);
                Project project = Repository.GetProject(form.ProjectId);
                if (template == null) continue;

                if (Contains(needle, template.DocNumber, template.DocName, form.QcRecordNo,
                             Format.StatusText(form.Status), project != null ? project.Name : null))
                {
                    string formId = form.Id;
                    results.Add(new ResultEntry
                    {
                        Type = "Form",
                        Glyph = Glyph.Clipboard,
                        Label = template.DocNumber + " — " + template.DocName,
                        Detail = (project != null ? project.Name + " · " : string.Empty) + form.QcRecordNo,
                        Status = form.Status,
                        Navigate = () => Shell.Navigate(new FormFillView(Shell, formId))
                    });
                }
            }

            foreach (CableRegister register in Repository.Db.Registers)
            {
                Project project = Repository.GetProject(register.ProjectId);
                if (Contains(needle, CableRegister.DocNumber, CableRegister.DocName,
                             register.QcRecordNo, project != null ? project.Name : null))
                {
                    string registerId = register.Id;
                    results.Add(new ResultEntry
                    {
                        Type = "Register",
                        Glyph = Glyph.Cable,
                        Label = CableRegister.DocNumber + " — " + CableRegister.DocName,
                        Detail = (project != null ? project.Name + " · " : string.Empty) + register.QcRecordNo,
                        Status = register.Status,
                        Navigate = () => Shell.Navigate(new RegisterView(Shell, registerId))
                    });
                }
            }

            foreach (ChecklistTemplate template in Repository.Db.Templates)
            {
                if (Contains(needle, template.DocNumber, template.DocName, template.Description))
                {
                    string templateId = template.Id;
                    results.Add(new ResultEntry
                    {
                        Type = "Checklist Template",
                        Glyph = Glyph.Tool,
                        Label = template.DocNumber + " — " + template.DocName,
                        Detail = template.Description ?? string.Empty,
                        Navigate = Repository.IsAdmin
                            ? (Action)(() => Shell.Navigate(new TemplateEditorView(Shell, templateId)))
                            : (Action)(() => Shell.Navigate(new LibraryView(Shell)))
                    });
                }
            }

            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            string title = results.Count + (results.Count == 1 ? " result" : " results")
                           + " for \"" + _initialQuery.Trim() + "\"";
            Card bar = Ui.SectionBar(title, InnerWidth);
            card.Controls.Add(bar);

            int rowY = bar.Height;

            if (results.Count == 0)
            {
                Label empty = Ui.Muted("No matches found.");
                empty.Location = new Point(20, rowY + 20);
                card.Controls.Add(empty);
                rowY += 60;
            }
            else
            {
                for (int i = 0; i < results.Count; i++)
                {
                    ResultRow row = new ResultRow
                    {
                        Entry = results[i],
                        ShowDivider = i < results.Count - 1,
                        Location = new Point(1, rowY),
                        Width = InnerWidth - 2,
                        Height = Dpi.S(62)
                    };

                    Action nav = results[i].Navigate;
                    row.Click += (s, e) => nav();

                    card.Controls.Add(row);
                    rowY += row.Height;
                }
            }

            card.Height = rowY + 2;
            Controls.Add(card);
            Y = card.Bottom + 8;
        }

        private static bool Contains(string needle, params string[] haystack)
        {
            foreach (string candidate in haystack)
            {
                if (!string.IsNullOrEmpty(candidate)
                    && candidate.ToLowerInvariant().Contains(needle))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>One search hit.</summary>
        internal sealed class ResultEntry
        {
            public string Type { get; set; }
            public Glyph Glyph { get; set; }
            public string Label { get; set; }
            public string Detail { get; set; }
            public RecordStatus? Status { get; set; }
            public Action Navigate { get; set; }
        }

        /// <summary>Owner-drawn search result row with icon, type, label and status.</summary>
        private sealed class ResultRow : Control
        {
            private bool _hovered;

            public ResultRow()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer
                         | ControlStyles.AllPaintingInWmPaint
                         | ControlStyles.UserPaint
                         | ControlStyles.ResizeRedraw
                         | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
            }

            public ResultEntry Entry { get; set; }
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
                if (Entry == null) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                if (_hovered)
                {
                    using (SolidBrush hb = new SolidBrush(Theme.Slate50))
                    {
                        g.FillRectangle(hb, 0, 0, Width, Height);
                    }
                }

                // Icon chip.
                Rectangle chip = new Rectangle(18, (Height - 36) / 2, 36, 36);
                using (GraphicsPath path = Card.Build(chip, Theme.RadiusSm, false, false))
                using (SolidBrush cb = new SolidBrush(Theme.Slate50))
                using (Pen cp = new Pen(Theme.Slate150))
                {
                    g.FillPath(cb, path);
                    g.DrawPath(cp, path);
                }
                Glyphs.Draw(g, Entry.Glyph,
                    new Rectangle(chip.X + 10, chip.Y + 10, 16, 16), Theme.Navy700, 1.7f);

                // Trailing status pill and chevron.
                int right = Width - 18;

                Glyphs.Draw(g, Glyph.ChevronRight,
                    new Rectangle(right - 16, (Height - 16) / 2, 16, 16), Theme.Slate400, 1.7f);
                right -= 26;

                if (Entry.Status.HasValue)
                {
                    RecordStatus status = Entry.Status.Value;
                    string text = Format.StatusText(status);

                    using (Font pf = Theme.TinyBold)
                    {
                        SizeF size = g.MeasureString(text, pf);
                        int w = (int)Math.Ceiling(size.Width) + 34;
                        int h = 22;
                        Rectangle pill = new Rectangle(right - w, (Height - h) / 2, w, h);

                        using (GraphicsPath path = Card.Build(pill, h / 2, false, false))
                        using (SolidBrush pb = new SolidBrush(Format.StatusBack(status)))
                        {
                            g.FillPath(pb, path);
                        }

                        Color fore = Format.StatusFore(status);
                        Glyph icon = status == RecordStatus.InProgress ? Glyph.Clock
                                   : status == RecordStatus.CompleteAccepted ? Glyph.CheckCircle
                                   : Glyph.Alert;
                        Glyphs.Draw(g, icon,
                            new Rectangle(pill.X + 9, pill.Y + 5, 12, 12), fore, 1.7f);

                        using (SolidBrush tb = new SolidBrush(fore))
                        using (StringFormat fmt = new StringFormat())
                        {
                            fmt.LineAlignment = StringAlignment.Center;
                            fmt.FormatFlags = StringFormatFlags.NoWrap;
                            g.DrawString(text, pf, tb,
                                new Rectangle(pill.X + 24, pill.Y, pill.Width - 28, h), fmt);
                        }

                        right = pill.X - 12;
                    }
                }

                int textWidth = Math.Max(60, right - 66);

                using (Font tf = Theme.Label)
                using (SolidBrush tb = new SolidBrush(Theme.Slate400))
                {
                    g.DrawString(Theme.Track(Entry.Type.ToUpperInvariant()), tf, tb, 66, 11);
                }

                using (Font lf = Theme.SmallBold)
                using (SolidBrush lb = new SolidBrush(Theme.Slate900))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.FormatFlags = StringFormatFlags.NoWrap;
                    fmt.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(Entry.Label, lf, lb, new RectangleF(66, 25, textWidth, 18), fmt);
                }

                using (Font df = Theme.Tiny)
                using (SolidBrush db = new SolidBrush(Theme.Slate600))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.FormatFlags = StringFormatFlags.NoWrap;
                    fmt.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(Entry.Detail, df, db, new RectangleF(66, 42, textWidth, 16), fmt);
                }

                if (ShowDivider)
                {
                    using (Pen p = new Pen(Theme.Slate100))
                    {
                        g.DrawLine(p, 0, Height - 1, Width, Height - 1);
                    }
                }
            }
        }
    }
}
