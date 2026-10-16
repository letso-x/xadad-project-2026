using System;
using System.Drawing;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Forms;
using MzuApplication.Models;

namespace MzuApplication.Views
{
    /// <summary>
    /// Organisation settings: branding, document numbering and the QC policy switches
    /// that the submission rules read.
    /// </summary>
    public class SettingsView : ViewBase
    {
        public SettingsView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "settings"; }
        }

        protected override void BuildContent()
        {
            AddPageHeader("Preferences", "Settings", "Shared authentication and quality policy are enforced by the service. Organisation settings are not exposed for editing.");
            var label = Ui.Body("Local inactivity timeout (minutes)"); label.Location = new Point(Left1,Y); Controls.Add(label); Y = label.Bottom + 8;
            var value = new TextInput { Value = LocalPreferences.IdleMinutes.ToString(), Width = 200, Location = new Point(Left1,Y) }; Controls.Add(value); Y = value.Bottom + 12;
            var save = new FlatButton { Text = "Save local preference", Width = 220, Location = new Point(Left1,Y) };save.Click += (s,e) => { int minutes; if(!int.TryParse(value.Value,out minutes)){Notify("Enter a number of minutes.",true);return;}try{LocalPreferences.Save(minutes);Notify("Local preference saved.");}catch(Exception ex){Notify(ex.Message,true);} };Controls.Add(save);Y=save.Bottom+20;
            var password = new FlatButton { Text = "Change password", Width = 220, Location = new Point(Left1,Y) }; password.Click += (s,e) => { using(var dialog=new ChangePasswordForm())dialog.ShowDialog(Shell); }; Controls.Add(password); Y=password.Bottom+20;
        }
    }
}
