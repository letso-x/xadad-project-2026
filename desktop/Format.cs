using System;
using System.Drawing;
using System.Globalization;
using MzuApplication.Models;

namespace MzuApplication
{
    /// <summary>Shared display formatting for dates, statuses and initials.</summary>
    internal static class Format
    {
        private static readonly CultureInfo Za = CultureInfo.GetCultureInfo("en-ZA");

        public static string Date(DateTime? value)
        {
            if (!value.HasValue) return "—";
            return value.Value.ToString("dd MMM yyyy", Za);
        }

        public static string DateTimeLong(DateTime? value)
        {
            if (!value.HasValue) return "—";
            return value.Value.ToString("dd MMM yyyy", Za) + " · " + value.Value.ToString("HH:mm", Za);
        }

        /// <summary>ISO date for editable inputs.</summary>
        public static string Iso(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;
        }

        public static string StatusText(RecordStatus status)
        {
            switch (status)
            {
                case RecordStatus.CompleteAccepted: return "Complete — Accepted";
                case RecordStatus.CompleteNcrRaised: return "Complete — NCR Raised";
                default: return "In Progress";
            }
        }

        public static Color StatusBack(RecordStatus status)
        {
            switch (status)
            {
                case RecordStatus.CompleteAccepted: return Theme.Green100;
                case RecordStatus.CompleteNcrRaised: return Theme.Red100;
                default: return Theme.Amber100;
            }
        }

        public static Color StatusFore(RecordStatus status)
        {
            switch (status)
            {
                case RecordStatus.CompleteAccepted: return Theme.Green700;
                case RecordStatus.CompleteNcrRaised: return Theme.Red700;
                default: return Theme.Amber700;
            }
        }

        public static Color StatusDot(RecordStatus status)
        {
            switch (status)
            {
                case RecordStatus.CompleteAccepted: return Theme.Green600;
                case RecordStatus.CompleteNcrRaised: return Theme.Red600;
                default: return Theme.Amber600;
            }
        }

        /// <summary>Colour pair for a project status pill.</summary>
        public static void ProjectStatusColors(string status, out Color back, out Color fore)
        {
            switch (status)
            {
                case "Active":
                    back = Theme.Green100; fore = Theme.Green700; break;
                case "Complete":
                    back = Theme.Green100; fore = Theme.Green700; break;
                case "On Hold":
                    back = Theme.Amber100; fore = Theme.Amber700; break;
                default:
                    back = Theme.Slate150; fore = Theme.Slate800; break;
            }
        }

        public static string ResponseCode(ItemResponse response)
        {
            switch (response)
            {
                case ItemResponse.Accepted: return "A";
                case ItemResponse.Rejected: return "R";
                case ItemResponse.NotApplicable: return "N/A";
                default: return string.Empty;
            }
        }

        public static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";

            string[] parts = name.Replace(".", " ")
                                 .Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1)
            {
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            }

            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
        }

        public static string RoleName(UserRole role)
        {
            return role == UserRole.Admin ? "Administrator" : "Site User";
        }

        public static string RoleBadge(UserRole role)
        {
            return role == UserRole.Admin ? "ADMIN" : "USER";
        }

        public static string NcrStateText(NcrState state)
        {
            switch (state)
            {
                case NcrState.Open: return "Open";
                case NcrState.InRemediation: return "In Remediation";
                case NcrState.AwaitingVerification: return "Awaiting Verification";
                case NcrState.Closed: return "Closed";
                default: return state.ToString();
            }
        }

        /// <summary>Colour pair for an NCR state pill.</summary>
        public static void NcrStateColors(NcrState state, out Color back, out Color fore)
        {
            switch (state)
            {
                case NcrState.Closed:
                    back = Theme.Green100; fore = Theme.Green700; break;
                case NcrState.AwaitingVerification:
                    back = Theme.Amber100; fore = Theme.Amber700; break;
                case NcrState.InRemediation:
                    back = Theme.Amber100; fore = Theme.Amber700; break;
                default:
                    back = Theme.Red100; fore = Theme.Red700; break;
            }
        }

        /// <summary>Colour pair for an NCR severity pill.</summary>
        public static void SeverityColors(NcrSeverity severity, out Color back, out Color fore)
        {
            switch (severity)
            {
                case NcrSeverity.Critical:
                    back = Theme.Red100; fore = Theme.Red700; break;
                case NcrSeverity.Major:
                    back = Theme.Amber100; fore = Theme.Amber700; break;
                default:
                    back = Theme.Slate150; fore = Theme.Slate700; break;
            }
        }

        /// <summary>Relative time for activity feeds, e.g. "3 h ago".</summary>
        public static string Relative(DateTime when)
        {
            TimeSpan gap = DateTime.Now - when;

            if (gap.TotalSeconds < 0) return Date(when);
            if (gap.TotalMinutes < 1) return "just now";
            if (gap.TotalMinutes < 60) return (int)gap.TotalMinutes + " min ago";
            if (gap.TotalHours < 24) return (int)gap.TotalHours + " h ago";
            if (gap.TotalDays < 7) return (int)gap.TotalDays + " d ago";

            return Date(when);
        }
    }
}
