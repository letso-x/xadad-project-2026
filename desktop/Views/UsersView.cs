using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Forms;
using MzuApplication.Models;

namespace MzuApplication.Views
{
    /// <summary>
    /// Administrator user management: lists every account and provides reset-password,
    /// deregister (disable), reactivate and delete actions. Resetting a password forces
    /// the user to choose a new one at their next sign-in.
    /// </summary>
    public class UsersView : ViewBase
    {
        private DataTable _table;
        private TextInput _filter;

        public UsersView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "users"; }
        }

        protected override void BuildContent()
        {
            if (!Repository.IsAdmin)
            {
                TemplatesView.AddAccessDenied(this);
                return;
            }

            AddPageHeader("Administration", "Users",
                "Everyone enrolled in the system. Reset passwords or deregister accounts.");

            AddSummary();

            _filter = new TextInput
            {
                Glyph = Glyph.Search,
                Placeholder = "Filter by name, username or email...",
                Location = new Point(Left1, Y),
                Width = Dpi.S(300),
                Height = Dpi.S(34)
            };
            _filter.ValueChanged += (s, e) => _table.ApplyFilter(_filter.Value);
            Controls.Add(_filter);
            Y = _filter.Bottom + Dpi.S(14);

            _table = new DataTable
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                EmptyMessage = "No users enrolled yet."
            };
            _table.DefineColumns("User", 2.4f, "Username", 1.4f, "Role", 1.0f,
                                 "Status", 1.2f, "Last sign-in", 1.4f, "Actions", 2.2f);
            _table.RowActivated += (s, tag) => ShowActions(tag as UserAccount);

            LoadRows();
            Controls.Add(_table);
            Y = _table.Bottom + Dpi.S(8);

            Label hint = Ui.Wrapped(
                "Click a row to manage that account. Deregistering disables sign-in without "
                + "deleting the user's history; deleting removes the account entirely.",
                Theme.Tiny, Theme.Slate500, InnerWidth);
            hint.Location = new Point(Left1, Y);
            Controls.Add(hint);
            Y = hint.Bottom + Dpi.S(10);
        }

        private void AddSummary()
        {
            var users = Repository.Db.Users;
            int total = users.Count;
            int admins = 0, disabled = 0;
            foreach (UserAccount u in users)
            {
                if (u.Role == UserRole.Admin) admins++;
                if (!u.Active) disabled++;
            }

            var tiles = new List<Tuple<Glyph, string, string, bool>>
            {
                Tuple.Create(Glyph.Users, total.ToString(), "Total Users", false),
                Tuple.Create(Glyph.Shield, admins.ToString(), "Administrators", false),
                Tuple.Create(Glyph.Alert, disabled.ToString(), "Deregistered", disabled > 0)
            };

            const int perRow = 3;
            int gap = Dpi.S(14);
            int height = Dpi.S(92);
            int width = (InnerWidth - gap * (perRow - 1)) / perRow;

            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                Card tile = StatTile.Build(t.Item1, t.Item2, t.Item3, t.Item4, width, height);
                tile.Location = new Point(Left1 + i * (width + gap), Y);
                Controls.Add(tile);
            }

            Y += height + Dpi.S(22);
        }

        private void LoadRows()
        {
            var rows = new List<TableRow>();

            foreach (UserAccount user in Auth.AllUsers())
            {
                bool isAdmin = user.Role == UserRole.Admin;

                TableRow row = new TableRow { Tag = user };
                row.Cells.Add(new TableCell(user.FullName)
                {
                    Bold = true,
                    SubText = string.IsNullOrEmpty(user.Email) ? null : user.Email
                });
                row.Cells.Add(new TableCell(user.Username) { Mono = true });
                row.Cells.Add(new TableCell(isAdmin ? "ADMIN" : "USER")
                {
                    PillBack = isAdmin ? Theme.Brick100 : Theme.Slate150,
                    PillFore = isAdmin ? Theme.Brick700 : Theme.Slate700
                });

                string statusText = !user.Active ? "Deregistered"
                                   : user.MustChangePassword ? "Reset pending"
                                   : "Active";
                Color back = !user.Active ? Theme.Red100
                           : user.MustChangePassword ? Theme.Amber100
                           : Theme.Green100;
                Color fore = !user.Active ? Theme.Red700
                           : user.MustChangePassword ? Theme.Amber700
                           : Theme.Green700;
                row.Cells.Add(new TableCell(statusText) { PillBack = back, PillFore = fore });

                row.Cells.Add(new TableCell(
                    user.LastLoginAt.HasValue ? Format.Relative(user.LastLoginAt.Value) : "never")
                { Muted = true });

                row.Cells.Add(new TableCell("Manage") { Muted = true });

                row.FilterText = (user.FullName + " " + user.Username + " " + user.Email)
                    .ToLowerInvariant();
                rows.Add(row);
            }

            _table.SetRows(rows);
        }

        /// <summary>Opens the manage dialog for one account.</summary>
        private void ShowActions(UserAccount user)
        {
            if (user == null) return;

            bool isSelf = Auth.Current != null && Auth.Current.Id == user.Id;

            using (Dialog dialog = new Dialog("Manage — " + user.FullName, Dpi.S(500)))
            {
                dialog.AddReadOnlyField("Username", user.Username);
                dialog.AddReadOnlyField("Email", string.IsNullOrEmpty(user.Email) ? "—" : user.Email);
                dialog.AddReadOnlyField("Role", Format.RoleName(user.Role));
                dialog.AddReadOnlyField("Status",
                    !user.Active ? "Deregistered"
                    : user.MustChangePassword ? "Active — password reset pending"
                    : "Active");
                dialog.AddReadOnlyField("Enrolled", Format.Date(user.CreatedAt));
                dialog.AddReadOnlyField("Last sign-in",
                    user.LastLoginAt.HasValue ? Format.DateTimeLong(user.LastLoginAt.Value) : "Never");

                if (isSelf)
                {
                    dialog.AddNote("This is your own account, so administrative actions "
                                   + "that would lock you out are not available here.");
                }

                // Reset password — not for yourself (use normal change flow instead).
                if (!isSelf)
                {
                    dialog.AddPrimaryAction("Reset Password",
                        () => { dialog.DialogResult = DialogResult.Retry; });
                }

                // Deregister / reactivate.
                if (!isSelf)
                {
                    if (user.Active)
                    {
                        dialog.AddDangerAction("Deregister",
                            () => { dialog.DialogResult = DialogResult.No; });
                    }
                    else
                    {
                        dialog.AddPrimaryAction("Reactivate",
                            () => { dialog.DialogResult = DialogResult.Yes; });
                    }

                    dialog.AddDangerAction("Delete",
                        () => { dialog.DialogResult = DialogResult.Abort; });
                }

                DialogResult outcome = dialog.ShowDialog(Shell);

                switch (outcome)
                {
                    case DialogResult.Retry: ResetPassword(user); break;
                    case DialogResult.No: Deregister(user); break;
                    case DialogResult.Yes: Reactivate(user); break;
                    case DialogResult.Abort: DeleteUser(user); break;
                }
            }
        }

        private void ResetPassword(UserAccount user)
        {
            string temp = Auth.GenerateTemporaryPassword();
            Auth.ResetPassword(user, temp);

            // Show the temporary password so the admin can pass it to the user. In a
            // real deployment this would be emailed instead of shown.
            MessageBox.Show(Shell,
                "Temporary password for " + user.FullName + ":\r\n\r\n    " + temp
                + "\r\n\r\nShare this with the user securely. They will be required to set "
                + "a new password the first time they sign in.",
                "Password reset", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Notify("Password reset for " + user.Username + ".");
            Rebuild();
        }

        private void Deregister(UserAccount user)
        {
            if (MessageBox.Show(Shell,
                    "Deregister " + user.FullName + "? They will no longer be able to sign in, "
                    + "but their records and history are kept.",
                    "Deregister user", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                != DialogResult.Yes)
            {
                return;
            }

            Auth.SetActive(user, false);
            Notify(user.Username + " has been deregistered.");
            Rebuild();
        }

        private void Reactivate(UserAccount user)
        {
            Auth.SetActive(user, true);
            Notify(user.Username + " has been reactivated.");
            Rebuild();
        }

        private void DeleteUser(UserAccount user)
        {
            if (MessageBox.Show(Shell,
                    "Permanently delete " + user.FullName + "? This cannot be undone.",
                    "Delete user", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2)
                != DialogResult.Yes)
            {
                return;
            }

            string error;
            if (Auth.Delete(user, out error))
            {
                Notify(user.Username + " has been deleted.");
                Rebuild();
            }
            else
            {
                Notify(error, true);
            }
        }
    }
}
