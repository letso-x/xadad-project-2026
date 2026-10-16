using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>Outcome of verifying an MFA code.</summary>
    internal enum MfaResult
    {
        Success = 0,
        Incorrect = 1,
        Expired = 2,
        TooManyAttempts = 3,
        NotRequested = 4
    }

    /// <summary>
    /// Email-based multi-factor authentication. After a correct password, a one-time
    /// 6-digit code is generated, emailed to the user, and must be entered to complete
    /// sign-in. Codes are held only as a salted hash in memory with a short expiry and
    /// a limited number of attempts — they are never stored in plain text or persisted.
    /// </summary>
    internal static class MfaService
    {
        private const int CodeLength = 6;
        private const int ValidityMinutes = 5;
        private const int MaxAttempts = 5;

        private sealed class Challenge
        {
            public string Hash;
            public string Salt;
            public DateTime Expires;
            public int Attempts;
        }

        // Keyed by user id. A user has at most one active challenge at a time.
        private static readonly Dictionary<string, Challenge> _pending =
            new Dictionary<string, Challenge>();

        /// <summary>
        /// Generates a code, stores its hash, and emails it to the account. Returns null
        /// on success, or an error message if the email could not be sent.
        /// </summary>
        public static string Issue(UserAccount account)
        {
            if (account == null) return "No account.";

            string code = GenerateCode();

            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);

            _pending[account.Id] = new Challenge
            {
                Salt = Convert.ToBase64String(salt),
                Hash = HashCode(code, salt),
                Expires = DateTime.UtcNow.AddMinutes(ValidityMinutes),
                Attempts = 0
            };

            string body =
                "Hello " + (string.IsNullOrWhiteSpace(account.FullName) ? "there" : account.FullName) + ",\r\n\r\n"
                + "Your Mzukulu QMS verification code is:\r\n\r\n"
                + "    " + code + "\r\n\r\n"
                + "This code expires in " + ValidityMinutes + " minutes. If you did not try to "
                + "sign in, you can ignore this message.\r\n\r\n"
                + "— Mzukulu QMS";

            string error = EmailSender.Send(account.Email, "Your Mzukulu QMS verification code", body);
            return error; // null on success
        }

        /// <summary>Checks a submitted code against the pending challenge for the user.</summary>
        public static MfaResult Verify(UserAccount account, string code)
        {
            if (account == null) return MfaResult.NotRequested;

            Challenge c;
            if (!_pending.TryGetValue(account.Id, out c)) return MfaResult.NotRequested;

            if (DateTime.UtcNow > c.Expires)
            {
                _pending.Remove(account.Id);
                return MfaResult.Expired;
            }

            if (c.Attempts >= MaxAttempts)
            {
                _pending.Remove(account.Id);
                return MfaResult.TooManyAttempts;
            }

            c.Attempts++;

            string attempt = HashCode((code ?? string.Empty).Trim(),
                Convert.FromBase64String(c.Salt));

            if (FixedTimeEquals(attempt, c.Hash))
            {
                _pending.Remove(account.Id);
                return MfaResult.Success;
            }

            return MfaResult.Incorrect;
        }

        /// <summary>Discards any pending challenge for the account.</summary>
        public static void Clear(UserAccount account)
        {
            if (account != null) _pending.Remove(account.Id);
        }

        public static int ValidityMinutesValue { get { return ValidityMinutes; } }

        private static string GenerateCode()
        {
            // Uniform 6-digit code (000000-999999) from a cryptographic RNG.
            byte[] b = new byte[4];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            uint value = BitConverter.ToUInt32(b, 0) % 1000000u;
            return value.ToString("D" + CodeLength);
        }

        private static string HashCode(string code, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(code, salt, 50000))
            {
                return Convert.ToBase64String(pbkdf2.GetBytes(32));
            }
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static string Describe(MfaResult result)
        {
            switch (result)
            {
                case MfaResult.Incorrect: return "That code is not correct. Please try again.";
                case MfaResult.Expired: return "That code has expired. Please request a new one.";
                case MfaResult.TooManyAttempts: return "Too many attempts. Please sign in again.";
                case MfaResult.NotRequested: return "No verification is pending.";
                default: return string.Empty;
            }
        }
    }
}
