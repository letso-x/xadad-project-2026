using System;
using System.Net;
using System.Net.Mail;

namespace MzuApplication.Data
{
    /// <summary>
    /// Sends email via SMTP using the settings in <see cref="EmailConfig"/>. Used to
    /// deliver multi-factor authentication codes. Returns a friendly error string on
    /// failure rather than throwing, so the sign-in flow can react gracefully.
    /// </summary>
    internal static class EmailSender
    {
        /// <summary>Sends a message. Returns null on success, or an error message.</summary>
        public static string Send(string toAddress, string subject, string body)
        {
            if (!EmailConfig.IsConfigured)
            {
                return "Email is not configured on this machine (email.config missing or incomplete).";
            }

            if (string.IsNullOrWhiteSpace(toAddress))
            {
                return "No destination email address is set for this account.";
            }

            try
            {
                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(EmailConfig.From, EmailConfig.FromName);
                    message.To.Add(new MailAddress(toAddress));
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = false;

                    using (var client = new SmtpClient(EmailConfig.Host, EmailConfig.Port))
                    {
                        client.EnableSsl = EmailConfig.EnableSsl;
                        client.DeliveryMethod = SmtpDeliveryMethod.Network;
                        client.UseDefaultCredentials = false;
                        client.Credentials = new NetworkCredential(
                            EmailConfig.User, EmailConfig.Password);
                        client.Timeout = 20000;

                        client.Send(message);
                    }
                }

                return null;
            }
            catch (SmtpException ex)
            {
                // Microsoft 365 commonly rejects basic-auth SMTP; surface a clear hint.
                return "Could not send the email: " + ex.Message
                    + (ex.StatusCode == SmtpStatusCode.MustIssueStartTlsFirst
                        || ex.Message.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0
                        ? " (The mail server may require modern authentication / SMTP AUTH may be disabled.)"
                        : string.Empty);
            }
            catch (Exception ex)
            {
                return "Could not send the email: " + ex.Message;
            }
        }
    }
}
