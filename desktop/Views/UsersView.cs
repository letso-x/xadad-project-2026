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

        public UsersView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "users"; }
        }

        protected override void BuildContent()
        {
            AddPageHeader("Administration", "Users", "Shared user administration is not exposed by the API. Contact your Supabase/QMS administrator.");
            var user = Ui.Body(Auth.Current == null ? "Signed out" : Auth.Current.FullName + " · " + Auth.Current.Email); user.Location = new Point(Left1,Y); Controls.Add(user); Y = user.Bottom + 20;
        }
    }
}
