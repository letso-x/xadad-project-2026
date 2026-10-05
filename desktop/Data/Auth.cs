using System;
using System.Linq;
using System.Security.Cryptography;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>Outcome of a registration or sign-in attempt.</summary>
    internal enum AuthResult
    {
        Success = 0,
        InvalidCredentials = 1,
        UsernameTaken = 2,
        AccountDisabled = 3,
        WeakPassword = 4,
        MissingFields = 5
    }

    /// <summary>
    /// Account registration and sign-in.
    ///
    /// Passwords are hashed with PBKDF2 (SHA-256, 100k iterations) over a per-user
    /// random salt, so the stored database never contains a recoverable password.
    /// Verification is constant-time to avoid leaking information through timing.
    /// This is a demo-grade local store standing in for a real identity provider,
    /// but the hashing approach is production-shaped rather than a toy.
    /// </summary>
    internal static class Auth
    {
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private const int Iterations = 100_000;

        /// <summary>The currently signed-in account, or null.</summary>
        public static UserAccount Current { get; private set; }

        public static bool IsSignedIn
        {
            get { return Current != null; }
        }

        #region Hashing

        private static string Hash(string password, byte[] salt, int iterations)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                byte[] hash = pbkdf2.GetBytes(HashBytes);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>Length-independent comparison that does not short-circuit.</summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        #endregion

        #region Registration

        /// <summary>
        /// Enrolls a new account. Fails if the username is taken or the password is
        /// too weak. The first account ever created becomes an administrator so the
        /// system is usable out of the box; later accounts default to site user.
        /// </summary>
        public static AuthResult Register(string username, string fullName, string email,
                                          string password, out UserAccount created)
        {
            created = null;

            if (string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(fullName)
                || string.IsNullOrEmpty(password))
            {
                return AuthResult.MissingFields;
            }

            username = username.Trim();

            if (!IsPasswordAcceptable(password))
            {
                return AuthResult.WeakPassword;
            }

            if (FindUser(username) != null)
            {
                return AuthResult.UsernameTaken;
            }

            byte[] salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            bool firstAccount = UserCount() == 0;

            UserAccount account = new UserAccount
            {
                Id = Repository.NewId("usr"),
                Username = username,
                FullName = fullName.Trim(),
                Email = (email ?? string.Empty).Trim(),
                PasswordSalt = Convert.ToBase64String(salt),
                PasswordHash = Hash(password, salt, Iterations),
                HashIterations = Iterations,
                Role = firstAccount ? UserRole.Admin : UserRole.SiteUser,
                Active = true,
                CreatedAt = DateTime.Now
            };

            StoreNew(account);

            Audit.Log(AuditAction.Created, "User", account.Id, account.Username,
                "Account enrolled as " + Format.RoleName(account.Role));

            created = account;
            return AuthResult.Success;
        }

        /// <summary>
        /// Minimum password policy: at least 8 characters with at least one letter and
        /// one digit. Kept deliberately simple but non-trivial.
        /// </summary>
        public static bool IsPasswordAcceptable(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8) return false;

            bool hasLetter = password.Any(char.IsLetter);
            bool hasDigit = password.Any(char.IsDigit);

            return hasLetter && hasDigit;
        }

        public const string PasswordRule =
            "At least 8 characters, including a letter and a number.";

        /// <summary>
        /// Builds a fully-formed account with a hashed password, for seeding demo data.
        /// Does not touch the repository or audit log.
        /// </summary>
        public static UserAccount CreateSeedAccount(string id, string username,
            string fullName, string email, string password, UserRole role)
        {
            byte[] salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            return new UserAccount
            {
                Id = id,
                Username = username,
                FullName = fullName,
                Email = email,
                PasswordSalt = Convert.ToBase64String(salt),
                PasswordHash = Hash(password, salt, Iterations),
                HashIterations = Iterations,
                Role = role,
                Active = true,
                CreatedAt = DateTime.Now
            };
        }

        #endregion

        #region Sign-in

        /// <summary>Validates credentials and, on success, sets <see cref="Current"/>.</summary>
        public static AuthResult SignIn(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                return AuthResult.MissingFields;
            }

            UserAccount account = FindUser(username.Trim());
            if (account == null)
            {
                // Hash anyway so a missing user and a wrong password take similar time.
                Hash(password, new byte[SaltBytes], Iterations);
                return AuthResult.InvalidCredentials;
            }

            if (!account.Active)
            {
                return AuthResult.AccountDisabled;
            }

            byte[] salt = Convert.FromBase64String(account.PasswordSalt);
            string attempt = Hash(password, salt, account.HashIterations);

            if (!FixedTimeEquals(attempt, account.PasswordHash))
            {
                return AuthResult.InvalidCredentials;
            }

            account.LastLoginAt = DateTime.Now;
            Current = account;
            StoreUpdate(account);

            // Mirror into the Repository session the rest of the app already reads.
            Repository.SignIn(account.Role, account.FullName);

            Audit.Log(AuditAction.SignedIn, "Session", account.Id, account.FullName,
                "Signed in as " + Format.RoleName(account.Role));

            return AuthResult.Success;
        }

        public static void SignOut()
        {
            Current = null;
            Repository.SignOut();
        }

        /// <summary>True if the signed-in user must set a new password before continuing.</summary>
        public static bool CurrentMustChangePassword
        {
            get { return Current != null && Current.MustChangePassword; }
        }

        #endregion

        #region Account management (admin + self-service)

        /// <summary>All accounts, administrators first then by name.</summary>
        public static System.Collections.Generic.List<UserAccount> AllUsers()
        {
            var source = UsePostgres ? PostgresUserStore.GetAll() : Repository.Db.Users;

            return source
                .OrderByDescending(u => u.Role == UserRole.Admin)
                .ThenBy(u => u.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Enables or disables an account (deregister = disable).</summary>
        public static void SetActive(UserAccount account, bool active)
        {
            if (account == null) return;

            account.Active = active;
            StoreUpdate(account);

            Audit.Log(AuditAction.Updated, "User", account.Id, account.Username,
                active ? "Account re-activated" : "Account deregistered (disabled)");
        }

        /// <summary>
        /// Permanently removes an account. Guards against deleting the last active
        /// administrator so the system can never be locked out of admin access.
        /// </summary>
        public static bool Delete(UserAccount account, out string error)
        {
            error = null;
            if (account == null) { error = "No account selected."; return false; }

            if (account.Role == UserRole.Admin && CountActiveAdmins(exclude: account) == 0)
            {
                error = "You cannot remove the last administrator.";
                return false;
            }

            StoreDelete(account);

            Audit.Log(AuditAction.Deleted, "User", account.Id, account.Username,
                "Account permanently removed");
            return true;
        }

        /// <summary>
        /// Resets an account's password to a temporary one and flags it so the user is
        /// forced to choose a new password at their next sign-in.
        /// </summary>
        public static void ResetPassword(UserAccount account, string temporaryPassword)
        {
            if (account == null) return;

            SetPassword(account, temporaryPassword);
            account.MustChangePassword = true;
            StoreUpdate(account);

            Audit.Log(AuditAction.Updated, "User", account.Id, account.Username,
                "Password reset by administrator; change required at next sign-in");
        }

        /// <summary>
        /// Changes the signed-in user's own password (used by the forced-change flow and
        /// voluntary changes). Clears the must-change flag.
        /// </summary>
        public static AuthResult ChangeOwnPassword(string newPassword)
        {
            if (Current == null) return AuthResult.InvalidCredentials;
            if (!IsPasswordAcceptable(newPassword)) return AuthResult.WeakPassword;

            SetPassword(Current, newPassword);
            Current.MustChangePassword = false;
            StoreUpdate(Current);

            Audit.Log(AuditAction.Updated, "User", Current.Id, Current.Username,
                "Password changed");

            return AuthResult.Success;
        }

        /// <summary>Hashes and stores a new password with a fresh salt.</summary>
        private static void SetPassword(UserAccount account, string password)
        {
            byte[] salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            account.PasswordSalt = Convert.ToBase64String(salt);
            account.PasswordHash = Hash(password, salt, Iterations);
            account.HashIterations = Iterations;
        }

        private static int CountActiveAdmins(UserAccount exclude)
        {
            var source = UsePostgres ? PostgresUserStore.GetAll() : Repository.Db.Users;
            return source.Count(u =>
                u.Id != exclude.Id && u.Role == UserRole.Admin && u.Active);
        }

        /// <summary>Generates a readable temporary password that meets the policy.</summary>
        public static string GenerateTemporaryPassword()
        {
            // Avoids ambiguous characters (0/O, 1/l) for something a user must type once.
            const string letters = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
            const string digits = "23456789";

            var sb = new System.Text.StringBuilder();
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] b = new byte[10];
                rng.GetBytes(b);
                for (int i = 0; i < 6; i++) sb.Append(letters[b[i] % letters.Length]);
                for (int i = 6; i < 10; i++) sb.Append(digits[b[i] % digits.Length]);
            }
            return sb.ToString();
        }

        #endregion

        #region Storage abstraction (Postgres or local JSON)

        /// <summary>True when the app is configured to use Supabase Postgres.</summary>
        private static bool UsePostgres
        {
            get { return DbConfig.UsePostgres; }
        }

        private static UserAccount FindUser(string username)
        {
            if (UsePostgres)
            {
                return PostgresUserStore.FindByUsername(username);
            }

            return Repository.Db.Users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        }

        private static int UserCount()
        {
            return UsePostgres
                ? PostgresUserStore.GetAll().Count
                : Repository.Db.Users.Count;
        }

        private static void StoreNew(UserAccount account)
        {
            if (UsePostgres)
            {
                PostgresUserStore.Insert(account);
            }
            else
            {
                Repository.Db.Users.Add(account);
                Repository.Save();
            }
        }

        /// <summary>Persists changes to an existing account.</summary>
        private static void StoreUpdate(UserAccount account)
        {
            if (UsePostgres)
            {
                PostgresUserStore.Update(account);
            }
            else
            {
                Repository.Save();
            }
        }

        private static void StoreDelete(UserAccount account)
        {
            if (UsePostgres)
            {
                PostgresUserStore.Delete(account.Id);
            }
            else
            {
                Repository.Db.Users.Remove(account);
                Repository.Save();
            }
        }

        #endregion

        /// <summary>Human-readable message for an auth result.</summary>
        public static string Describe(AuthResult result)
        {
            switch (result)
            {
                case AuthResult.InvalidCredentials: return "Incorrect username or password.";
                case AuthResult.UsernameTaken: return "That username is already taken.";
                case AuthResult.AccountDisabled: return "This account has been disabled.";
                case AuthResult.WeakPassword: return PasswordRule;
                case AuthResult.MissingFields: return "Please complete all required fields.";
                default: return string.Empty;
            }
        }
    }
}
