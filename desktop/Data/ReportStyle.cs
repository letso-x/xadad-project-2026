using System;

namespace MzuApplication.Data
{
    /// <summary>
    /// Shared branding for the HTML reports (audit trail, quality) so they look identical:
    /// the Mzukulu colour palette, the inline-SVG logo, the header band and the base CSS.
    /// </summary>
    internal static class ReportStyle
    {
        public const string Navy = "#0F2340";
        public const string NavyDeep = "#0A1526";
        public const string Brick = "#A83F31";
        public const string Red = "#C4453A";
        public const string Green = "#2C8A5A";
        public const string Amber = "#AD7A20";
        public const string Slate900 = "#171E24";
        public const string Slate600 = "#54626F";
        public const string Slate400 = "#8C99A6";
        public const string Slate150 = "#E1E5EA";
        public const string Slate50 = "#F4F6F8";

        /// <summary>The Mzukulu "M" mark as inline SVG.</summary>
        public static string LogoSvg()
        {
            return
                "<svg class=\"logo\" viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">"
                + "<rect x=\"2\" y=\"2\" width=\"96\" height=\"96\" rx=\"20\" fill=\"" + Navy + "\"/>"
                + "<polygon points=\"26,26 42,26 56,70 50,74 26,74\" fill=\"" + Red + "\"/>"
                + "<polygon points=\"74,26 58,26 50,52 55,52 58,74 74,74\" fill=\"#ffffff\"/>"
                + "</svg>";
        }

        /// <summary>Header band with the logo, brand name and a report title.</summary>
        public static string Band(string reportTitle)
        {
            return "<div class=\"band\"><div class=\"brand\">" + LogoSvg()
                + "<div class=\"brandtext\"><div class=\"bname\">Mzukulu QMS</div>"
                + "<div class=\"btag\">Quality Management System</div></div></div>"
                + "<div class=\"reporttitle\">" + Enc(reportTitle) + "</div></div>";
        }

        public static string MetaItem(string label, string value)
        {
            return "<div class=\"metaitem\"><div class=\"mlabel\">" + Enc(label)
                + "</div><div class=\"mvalue\">" + Enc(value) + "</div></div>";
        }

        public static string Enc(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;");
        }

        public static string SafeHost()
        {
            try { return Environment.MachineName; }
            catch (Exception) { return "unknown"; }
        }

        public static string SafeCompany()
        {
            try
            {
                string c = Repository.Db.Settings.CompanyName;
                return string.IsNullOrWhiteSpace(c) ? "Mzukulu Technologies (Pty) Ltd" : c;
            }
            catch (Exception) { return "Mzukulu Technologies (Pty) Ltd"; }
        }

        /// <summary>Base stylesheet shared by all reports. <paramref name="landscape"/>
        /// chooses A4 orientation for print.</summary>
        public static string Css(bool landscape)
        {
            string page = landscape ? "A4 landscape" : "A4 portrait";
            return
"* { box-sizing: border-box; }"
+ "body { font-family:'Segoe UI',Arial,sans-serif; color:" + Slate900 + "; margin:0; background:#fff; }"
+ ".band { background:linear-gradient(135deg," + NavyDeep + "," + Navy + "); color:#fff; padding:28px 36px; display:flex; align-items:center; justify-content:space-between; }"
+ ".brand { display:flex; align-items:center; gap:16px; }"
+ ".logo { width:52px; height:52px; }"
+ ".bname { font-size:22px; font-weight:700; letter-spacing:.2px; }"
+ ".btag { font-size:12px; color:#9FB0C6; letter-spacing:1.5px; text-transform:uppercase; margin-top:2px; }"
+ ".reporttitle { font-size:18px; font-weight:600; color:#DCE4EE; }"
+ ".meta { display:flex; flex-wrap:wrap; padding:20px 36px; border-bottom:2px solid " + Slate150 + "; background:" + Slate50 + "; }"
+ ".metaitem { min-width:170px; padding:8px 24px 8px 0; }"
+ ".mlabel { font-size:10px; text-transform:uppercase; letter-spacing:1px; color:" + Slate400 + "; margin-bottom:3px; }"
+ ".mvalue { font-size:14px; font-weight:600; color:" + Slate900 + "; }"
+ ".badge { display:inline-block; padding:3px 10px; border-radius:12px; font-size:12px; font-weight:700; }"
+ ".badge.ok { background:#DBEFE1; color:" + Green + "; }"
+ ".badge.bad { background:#F6DBD8; color:" + Red + "; }"
+ ".section { padding:8px 36px 0; }"
+ ".sectitle { font-size:13px; font-weight:700; color:" + Navy + "; text-transform:uppercase; letter-spacing:.6px; margin:22px 0 10px; border-left:4px solid " + Brick + "; padding-left:10px; }"
+ ".kpis { display:flex; flex-wrap:wrap; gap:14px; padding:4px 36px 0; }"
+ ".kpi { flex:1; min-width:150px; border:1px solid " + Slate150 + "; border-radius:10px; padding:14px 16px; background:" + Slate50 + "; }"
+ ".kpi .num { font-size:26px; font-weight:800; color:" + Navy + "; }"
+ ".kpi .lab { font-size:12px; font-weight:600; color:" + Slate900 + "; margin-top:2px; }"
+ ".kpi .sub2 { font-size:11px; color:" + Slate400 + "; margin-top:2px; }"
+ ".kpi.flag .num { color:" + Red + "; }"
+ ".bars { padding:0 36px; }"
+ ".barrow { display:flex; align-items:center; gap:12px; margin:8px 0; }"
+ ".barrow .lab { width:150px; font-size:12px; font-weight:600; color:" + Slate900 + "; }"
+ ".track { flex:1; height:10px; background:" + Slate150 + "; border-radius:6px; overflow:hidden; }"
+ ".fill { height:100%; }"
+ ".barrow .val { width:120px; text-align:right; font-size:12px; font-family:'Consolas',monospace; color:" + Navy + "; }"
+ "table { width:100%; border-collapse:collapse; font-size:12px; }"
+ "thead th { background:" + Navy + "; color:#fff; text-align:left; padding:10px 12px; font-weight:600; font-size:11px; text-transform:uppercase; letter-spacing:.4px; }"
+ "tbody td { padding:9px 12px; border-bottom:1px solid " + Slate150 + "; vertical-align:top; }"
+ "tbody tr:nth-child(even) { background:" + Slate50 + "; }"
+ ".u { font-weight:600; color:" + Slate900 + "; }"
+ ".sub { font-size:10px; color:" + Slate400 + "; }"
+ ".mono { font-family:'Consolas','Courier New',monospace; }"
+ ".nowrap { white-space:nowrap; }"
+ ".pill { display:inline-block; padding:2px 9px; border-radius:10px; font-size:11px; font-weight:700; white-space:nowrap; }"
+ ".p-red { background:#F6DBD8; color:" + Red + "; }"
+ ".p-green { background:#DBEFE1; color:" + Green + "; }"
+ ".p-amber { background:#FAEBD0; color:" + Amber + "; }"
+ ".p-navy { background:#E3EAF4; color:" + Navy + "; }"
+ ".p-slate { background:#EBEEF1; color:" + Slate600 + "; }"
+ ".foot { padding:18px 36px 40px; color:" + Slate600 + "; font-size:11px; display:flex; justify-content:space-between; gap:24px; border-top:2px solid " + Slate150 + "; margin-top:18px; }"
+ ".footbrand { color:" + Slate400 + "; white-space:nowrap; }"
+ "@media print { body { -webkit-print-color-adjust:exact; print-color-adjust:exact; } * { -webkit-print-color-adjust:exact; print-color-adjust:exact; } @page { size:" + page + "; margin:12mm; } }";
        }
    }
}
