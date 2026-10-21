using System.Windows.Forms;
using MzuApplication.Controls;

namespace MzuApplication.Forms
{
    /// <summary>
    /// Branded replacements for the stock Windows MessageBox, built on the app's
    /// <see cref="Dialog"/> so every popup carries the Mzukulu look. Use these instead of
    /// MessageBox.Show so confirmations and notices stay on-brand.
    /// </summary>
    internal static class MessageDialog
    {
        /// <summary>A branded yes/no confirmation. Returns true if the user confirmed.</summary>
        public static bool Confirm(IWin32Window owner, string title, string message,
                                   string confirmText = "Confirm", bool danger = false)
        {
            using (Dialog dialog = new Dialog(title, Dpi.S(430)))
            {
                dialog.AddNote(message);

                if (danger)
                    dialog.AddDangerAction(confirmText, () => dialog.DialogResult = DialogResult.OK);
                else
                    dialog.AddPrimaryAction(confirmText, () => dialog.DialogResult = DialogResult.OK);

                return dialog.ShowDialog(owner) == DialogResult.OK;
            }
        }

        /// <summary>A branded information notice with a single acknowledge button.</summary>
        public static void Info(IWin32Window owner, string title, string message,
                                string okText = "OK")
        {
            using (Dialog dialog = new Dialog(title, Dpi.S(430)))
            {
                dialog.AddNote(message);
                dialog.AddPrimaryAction(okText, () => dialog.DialogResult = DialogResult.OK);
                dialog.ShowDialog(owner);
            }
        }
    }
}
