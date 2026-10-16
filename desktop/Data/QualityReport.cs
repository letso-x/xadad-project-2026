using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// A branded management "Quality Report" (HTML) mirroring the Reports screen: headline
    /// KPIs, inspection outcome mix, per-project roll-up and checklist usage. Shares the
    /// Mzukulu styling with the audit report and prints cleanly to PDF.
    /// </summary>
    internal static class QualityReport
    {
        public static void Export(string path)
        {
            string company = ReportStyle.SafeCompany();
            DateTime now = DateTime.Now;

            int accepted, finalised;
            int fpy = Analytics.FirstPassYield(out accepted, out finalised);
            int answered, totalItems;
            int completion = Analytics.OverallCompletion(out answered, out totalItems);
            int closedCount;
            double avgClose = Analytics.AverageNcrCloseDays(out closedCount);
            int openRej = Analytics.OpenRejections();

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
            sb.Append("<title>Mzukulu QMS — Quality Report</title>");
            sb.Append("<style>").Append(ReportStyle.Css(false)).Append("</style></head><body>");

            sb.Append(ReportStyle.Band("Quality Report"));

            sb.Append("<div class=\"meta\">");
            sb.Append(ReportStyle.MetaItem("Organisation", company));
            sb.Append(ReportStyle.MetaItem("Generated", now.ToString("dd MMMM yyyy, HH:mm")));
            sb.Append(ReportStyle.MetaItem("Prepared by", Repository.CurrentUserName ?? "system"));
            sb.Append(ReportStyle.MetaItem("Projects", Repository.Db.Projects.Count.ToString()));
            sb.Append("</div>");

            // ---- KPIs -------------------------------------------------------
            sb.Append("<div class=\"kpis\">");
            sb.Append(Kpi(fpy + "%", "First-pass yield",
                finalised == 0 ? "no finalised forms" : accepted + " of " + finalised + " accepted", false));
            sb.Append(Kpi(completion + "%", "Checklist completion",
                answered + " of " + totalItems + " lines", false));
            sb.Append(Kpi(openRej.ToString(), "Open rejections", "lines marked R", openRej > 0));
            sb.Append(Kpi(closedCount == 0 ? "—" : Math.Round(avgClose, 1).ToString("0.#"),
                "Avg. days to close NCR", closedCount + " closed", false));
            sb.Append("</div>");

            // ---- Outcome mix ------------------------------------------------
            int a2, r2, na2;
            Analytics.ResponseMix(out a2, out r2, out na2);
            int totalMix = a2 + r2 + na2;

            sb.Append("<div class=\"section\"><div class=\"sectitle\">Inspection Outcome Mix</div></div>");
            sb.Append("<div class=\"bars\">");
            if (totalMix == 0)
            {
                sb.Append("<div class=\"sub\" style=\"padding:4px 0 12px;\">No inspection results recorded yet.</div>");
            }
            else
            {
                sb.Append(Bar("Accepted", a2, totalMix, ReportStyle.Green));
                sb.Append(Bar("Rejected", r2, totalMix, ReportStyle.Red));
                sb.Append(Bar("Not applicable", na2, totalMix, ReportStyle.Slate400));
            }
            sb.Append("</div>");

            // ---- Project roll-up --------------------------------------------
            sb.Append("<div class=\"section\"><div class=\"sectitle\">Project Roll-Up</div>");
            sb.Append("<table><thead><tr><th>Project</th><th>Client</th><th>Records</th>"
                + "<th>Completion</th><th>Open NCRs</th></tr></thead><tbody>");

            foreach (Project project in Repository.Db.Projects)
            {
                Client client = Repository.GetClient(project.ClientId);
                int done, total;
                int percent = Repository.ProjectCompletionPercent(project.Id, out done, out total);
                int openNcrs = NcrService.ForProject(project.Id).Count(n => n.State != NcrState.Closed);

                sb.Append("<tr>");
                sb.Append("<td><div class=\"u\">").Append(ReportStyle.Enc(project.Name))
                  .Append("</div><div class=\"sub\">").Append(ReportStyle.Enc(project.Number ?? string.Empty))
                  .Append("</div></td>");
                sb.Append(Cell(client != null ? client.Name : "—", null));
                sb.Append(Cell(total.ToString(), null));
                sb.Append(Cell(percent + "%  (" + done + "/" + total + ")", "mono"));
                sb.Append("<td><span class=\"pill " + (openNcrs > 0 ? "p-red" : "p-green") + "\">"
                    + openNcrs + "</span></td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");

            // ---- Checklist usage --------------------------------------------
            sb.Append("<div class=\"section\"><div class=\"sectitle\">Checklist Usage</div>");
            sb.Append("<table><thead><tr><th>Doc No.</th><th>Checklist</th><th>Instances</th>"
                + "<th>Accepted</th><th>Acceptance rate</th></tr></thead><tbody>");

            foreach (var usage in Analytics.TemplateUsage())
            {
                ChecklistTemplate template = usage.Item1;
                int instances = usage.Item2;
                int acc = usage.Item3;
                int rate = instances == 0 ? 0 : (int)Math.Round(acc * 100.0 / instances);
                string pill = instances == 0 ? "p-slate" : rate >= 80 ? "p-green" : rate >= 50 ? "p-amber" : "p-red";

                sb.Append("<tr>");
                sb.Append(Cell(template.DocNumber, "mono"));
                sb.Append(Cell(template.DocName, null));
                sb.Append(Cell(instances.ToString(), null));
                sb.Append(Cell(acc.ToString(), null));
                sb.Append("<td><span class=\"pill " + pill + "\">"
                    + (instances == 0 ? "—" : rate + "%") + "</span></td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");

            sb.Append("<div class=\"foot\"><div>Management quality roll-up across all projects, "
                + "clients and checklists.</div><div class=\"footbrand\">" + ReportStyle.Enc(company)
                + " &middot; Generated " + now.ToString("yyyy-MM-dd HH:mm") + "</div></div>");

            sb.Append("</body></html>");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

            Audit.Log(AuditAction.Exported, "Report", null, "Quality Report",
                "Exported branded quality report");
        }

        private static string Kpi(string num, string label, string sub, bool flag)
        {
            return "<div class=\"kpi" + (flag ? " flag" : "") + "\"><div class=\"num\">"
                + ReportStyle.Enc(num) + "</div><div class=\"lab\">" + ReportStyle.Enc(label)
                + "</div><div class=\"sub2\">" + ReportStyle.Enc(sub) + "</div></div>";
        }

        private static string Bar(string label, int count, int total, string color)
        {
            int percent = total == 0 ? 0 : (int)Math.Round(count * 100.0 / total);
            return "<div class=\"barrow\"><div class=\"lab\">" + ReportStyle.Enc(label) + "</div>"
                + "<div class=\"track\"><div class=\"fill\" style=\"width:" + percent + "%;background:"
                + color + ";\"></div></div>"
                + "<div class=\"val\">" + percent + "%  (" + count + ")</div></div>";
        }

        private static string Cell(string text, string cls)
        {
            return cls == null
                ? "<td>" + ReportStyle.Enc(text) + "</td>"
                : "<td class=\"" + cls + "\">" + ReportStyle.Enc(text) + "</td>";
        }
    }
}
