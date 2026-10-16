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

        private object _portfolio;
        private bool _loading;
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_loading) { _loading = true; LoadReports(); }
        }
        private async void LoadReports()
        {
            try { await ReportingData.LoadPortfolioAsync(); _portfolio = ReportingData.Portfolio; Rebuild(); }
            catch (Exception ex) { Notify(ex.Message, true); }
        }
        protected override void BuildContent()
        {
            var refresh = new FlatButton { Text = "Refresh", Width = 120 }; refresh.Click += (s,e) => LoadReports();
            AddPageHeader("Insight", "Quality Reports", "Shared service metrics for accessible projects.", refresh);
            if (_portfolio == null) { var loading = Ui.Body("Report not loaded. Refresh to retry."); loading.Location = new Point(Left1,Y); Controls.Add(loading); Y = loading.Bottom + 20; return; }
            foreach (var item in new[] { Tuple.Create("totalProjects","Total projects"),Tuple.Create("activeProjects","Active projects"),Tuple.Create("totalQcRecords","QC records"),Tuple.Create("formsInProgress","Forms in progress"),Tuple.Create("approvedForms","Approved forms"),Tuple.Create("openNcrCount","Open NCRs") })
            {
                var label = Ui.Body(item.Item2 + ": " + (Json.Get(_portfolio,item.Item1) == null ? "Unavailable for this account" : Json.Text(_portfolio,item.Item1))); label.Location = new Point(Left1,Y); Controls.Add(label); Y = label.Bottom + 12;
            }
            foreach (var project in Repository.Db.Projects)
            {
                var report = new FlatButton { Text = project.Name + " — dashboard / handover", Width = Math.Min(InnerWidth,600), Location = new Point(Left1,Y) };
                report.Click += async (s,e) =>
                {
                    report.Enabled = false;
                    try
                    {
                        var dashboard = await ReportingData.DashboardAsync(project.Id);
                        using (var dialog = new Dialog(project.Name + " — quality report",650))
                        {
                            foreach (var status in Json.Rows(dashboard,"qcStatusCounts")) dialog.AddReadOnlyField(Json.Text(status,"status"),Json.Text(status,"count"));
                            dialog.AddReadOnlyField("Open NCRs",Json.Text(dashboard,"openNcrCount"));
                            dialog.AddReadOnlyField("ITP completion",Json.Text(Json.Get(Json.Get(dashboard,"itpProgress"),"progress"),"percentageComplete")+"%");
                            dialog.AddAsyncAction("Handover readiness",async()=>{var readiness=await ReportingData.ReadinessAsync(project.Id);var reasons=Json.Get(readiness,"reasons") as object[]??new object[0];MessageBox.Show(dialog,(Json.Bool(readiness,"isReady")?"Ready":"Not ready")+Environment.NewLine+string.Join(Environment.NewLine,reasons.Select(Convert.ToString)),"Handover readiness");});
                            dialog.ShowDialog(Shell);
                        }
                    }
                    catch (Exception ex) { Notify(ex.Message,true); }
                    finally { if(!report.IsDisposed)report.Enabled=true; }
                };
                Controls.Add(report); Y = report.Bottom + 10;
            }
        }
    }
}
