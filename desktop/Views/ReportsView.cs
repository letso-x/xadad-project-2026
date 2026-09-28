using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Forms;
using MzuApplication.Models;

namespace MzuApplication.Views
{
    /// <summary>
    /// Management reporting: quality KPIs, response mix, template usage and per-project
    /// roll-ups, with CSV export for handover packs.
    /// </summary>
    public class ReportsView : ViewBase
    {
        public ReportsView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "reports"; }
        }

        protected override void BuildContent()
        {
            AddPageHeader("Insight", "Quality Reports",
                "Roll-up measures across every project, client and checklist.");

            AddKpis();
            AddResponseMix();
            AddProjectRollup();
            AddTemplateUsage();
        }

        private void AddKpis()
        {
            int accepted, finalised;
            int fpy = Analytics.FirstPassYield(out accepted, out finalised);

            int answered, totalItems;
            int completion = Analytics.OverallCompletion(out answered, out totalItems);

            int closedCount;
            double avgClose = Analytics.AverageNcrCloseDays(out closedCount);

            var tiles = new List<Tuple<Glyph, string, string, string, bool>>
            {
                Tuple.Create(Glyph.CheckCircle, fpy + "%", "First-pass yield",
                    finalised == 0 ? "no finalised forms" : accepted + " of " + finalised + " accepted", false),
                Tuple.Create(Glyph.BarChart, completion + "%", "Checklist completion",
                    answered + " of " + totalItems + " lines", false),
                Tuple.Create(Glyph.Alert, Analytics.OpenRejections().ToString(), "Open rejections",
                    "lines marked R", Analytics.OpenRejections() > 0),
                Tuple.Create(Glyph.Clock,
                    closedCount == 0 ? "—" : Math.Round(avgClose, 1).ToString("0.#"),
                    "Avg. days to close NCR",
                    closedCount + " closed", false)
            };

            const int perRow = 4;
            int gap = Dpi.S(14);
            int height = Dpi.S(112);
            int width = (InnerWidth - gap * (perRow - 1)) / perRow;

            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                Card tile = StatTile.Build(t.Item1, t.Item2, t.Item3, t.Item5, width, height, t.Item4);
                tile.Location = new Point(Left1 + i * (width + gap), Y);
                Controls.Add(tile);
            }

            Y += height + Dpi.S(22);
        }

        /// <summary>Accepted / rejected / N-A split as proportional bars.</summary>
        private void AddResponseMix()
        {
            int accepted, rejected, na;
            Analytics.ResponseMix(out accepted, out rejected, out na);
            int total = accepted + rejected + na;

            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Inspection Outcome Mix", InnerWidth);
            card.Controls.Add(bar);

            int y = bar.Bottom + Dpi.S(18);

            if (total == 0)
            {
                Label empty = Ui.Muted("No inspection results recorded yet.");
                empty.Location = new Point(Dpi.S(20), y);
                card.Controls.Add(empty);
                y = empty.Bottom + Dpi.S(10);
            }
            else
            {
                y = AddMixRow(card, "Accepted", accepted, total, Theme.Green600, y);
                y = AddMixRow(card, "Rejected", rejected, total, Theme.Red600, y);
                y = AddMixRow(card, "Not applicable", na, total, Theme.Slate400, y);
            }

            card.Height = y + Dpi.S(8);
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(18);
        }

        private int AddMixRow(Card parent, string label, int count, int total,
                              Color color, int y)
        {
            int percent = total == 0 ? 0 : (int)Math.Round(count * 100.0 / total);

            Label caption = new Label
            {
                AutoSize = false,
                Font = Theme.SmallBold,
                ForeColor = Theme.Slate800,
                Location = new Point(Dpi.S(20), y),
                Width = Dpi.S(140),
                Height = Dpi.S(18),
                Text = label
            };
            parent.Controls.Add(caption);

            int trackLeft = caption.Right + Dpi.S(10);
            int trackWidth = parent.Width - trackLeft - Dpi.S(120);

            ProgressTrack track = new ProgressTrack
            {
                Location = new Point(trackLeft, y + Dpi.S(5)),
                Width = Math.Max(Dpi.S(60), trackWidth),
                Height = Dpi.S(8),
                Percent = percent,
                BarColor = color
            };
            parent.Controls.Add(track);

            Label value = new Label
            {
                AutoSize = true,
                Font = Theme.MonoSmall,
                ForeColor = Theme.Navy900,
                Text = percent + "%  (" + count + ")"
            };
            parent.Controls.Add(value);
            value.Location = new Point(parent.Width - value.Width - Dpi.S(20), y);

            return y + Dpi.S(30);
        }

        private void AddProjectRollup()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Project Roll-Up", InnerWidth);
            card.Controls.Add(bar);

            DataTable table = new DataTable
            {
                Location = new Point(1, bar.Height),
                Width = InnerWidth - 2,
                EmptyMessage = "No projects yet."
            };
            table.DefineColumns("Project", 2.4f, "Client", 1.4f, "Records", 0.9f,
                                "Complete", 1.0f, "Open NCRs", 1.0f, "Export", 1.0f);

            var rows = new List<TableRow>();

            foreach (Project project in Repository.Db.Projects)
            {
                Client client = Repository.GetClient(project.ClientId);

                int done, total;
                int percent = Repository.ProjectCompletionPercent(project.Id, out done, out total);
                int openNcrs = NcrService.ForProject(project.Id)
                    .Count(n => n.State != NcrState.Closed);

                TableRow row = new TableRow { Tag = project };
                row.Cells.Add(new TableCell(project.Name) { Bold = true, SubText = project.Number });
                row.Cells.Add(new TableCell(client != null ? client.Name : "—"));
                row.Cells.Add(new TableCell(total.ToString()));
                row.Cells.Add(new TableCell(percent + "%  (" + done + "/" + total + ")")
                {
                    Mono = true
                });
                row.Cells.Add(new TableCell(openNcrs.ToString())
                {
                    PillBack = openNcrs > 0 ? Theme.Red100 : Theme.Green100,
                    PillFore = openNcrs > 0 ? Theme.Red700 : Theme.Green700
                });
                row.Cells.Add(new TableCell("Click to export") { Muted = true });

                row.FilterText = project.Name.ToLowerInvariant();
                rows.Add(row);
            }

            table.SetRows(rows);
            table.RowActivated += (s, tag) =>
            {
                Project project = tag as Project;
                if (project != null) ExportProject(project);
            };
            card.Controls.Add(table);

            card.Height = bar.Height + table.Height + 2;
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(18);
        }

        private void AddTemplateUsage()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Checklist Usage", InnerWidth);
            card.Controls.Add(bar);

            DataTable table = new DataTable
            {
                Location = new Point(1, bar.Height),
                Width = InnerWidth - 2,
                EmptyMessage = "No templates yet."
            };
            table.DefineColumns("Doc No.", 1.0f, "Checklist", 2.8f, "Instances", 0.9f,
                                "Accepted", 0.9f, "Acceptance rate", 1.4f);

            var rows = new List<TableRow>();

            foreach (var usage in Analytics.TemplateUsage())
            {
                ChecklistTemplate template = usage.Item1;
                int instances = usage.Item2;
                int accepted = usage.Item3;
                int rate = instances == 0 ? 0 : (int)Math.Round(accepted * 100.0 / instances);

                TableRow row = new TableRow { Tag = template };
                row.Cells.Add(new TableCell(template.DocNumber) { Mono = true });
                row.Cells.Add(new TableCell(template.DocName));
                row.Cells.Add(new TableCell(instances.ToString()));
                row.Cells.Add(new TableCell(accepted.ToString()));
                row.Cells.Add(new TableCell(instances == 0 ? "—" : rate + "%")
                {
                    PillBack = instances == 0 ? Theme.Slate150
                             : rate >= 80 ? Theme.Green100
                             : rate >= 50 ? Theme.Amber100 : Theme.Red100,
                    PillFore = instances == 0 ? Theme.Slate600
                             : rate >= 80 ? Theme.Green700
                             : rate >= 50 ? Theme.Amber700 : Theme.Red700
                });

                rows.Add(row);
            }

            table.SetRows(rows);
            card.Controls.Add(table);

            card.Height = bar.Height + table.Height + 2;
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(8);
        }

        private void ExportProject(Project project)
        {
            using (SaveFileDialog save = new SaveFileDialog())
            {
                save.Filter = "CSV file (*.csv)|*.csv";
                save.FileName = Exporter.SafeFileName(project.Number + "-QC-register") + ".csv";

                if (save.ShowDialog(Shell) != DialogResult.OK) return;

                try
                {
                    Exporter.ExportProjectRegister(project, save.FileName);
                    Repository.Save();
                    Notify("Exported QC register for " + project.Name + ".");
                }
                catch (Exception ex)
                {
                    Notify("Export failed: " + ex.Message, true);
                }
            }
        }
    }
}
