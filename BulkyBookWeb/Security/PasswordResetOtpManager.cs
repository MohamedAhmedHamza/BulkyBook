using BulkyBookWeb.DataAccess.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BulkyBookWeb.Security
{
    public enum OtpResult { Ok, Invalid, Expired, TooManyAttempts }

    /// <summary>
    /// One-time passwords for password reset.
    ///  - 6 digits, generated with a cryptographic RNG.
    ///  - Unique: never equal to any other ACTIVE code (and never equal to the user's previous one).
    ///  - Only the HMAC hash is stored (AspNetUserTokens table - no migration needed).
    ///  - ONE active code per user: issuing a new code overwrites (= invalidates) the old one.
    ///  - Single use, 10 minutes lifetime, 5 wrong attempts max, 5 requests per hour per user.
    /// </summary>
    public class PasswordResetOtpManager
    {
        private const string Provider = "BulkyBook";
        private const string OtpName = "PwdResetOtp";
        private const string RateName = "PwdResetOtpRate";
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
        private const int MaxAttempts = 5;
        private const int MaxPerHour = 5;

        private readonly ApplicationDbContext _db;
        private readonly byte[] _key;

        public PasswordResetOtpManager(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            // Set "Otp:Secret" (User Secrets / environment) to a long random string in production.
            var secret = config["Otp:Secret"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                secret = "bulkybook-otp-default-secret-change-me";
            }
            _key = Encoding.UTF8.GetBytes(secret);
        }

        private string Hash(string code)
        {
            using var h = new HMACSHA256(_key);
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
        }

        /// <summary>Creates a NEW code for the user (old one stops working). Returns null when rate-limited.</summary>
        public async Task<string?> IssueAsync(IdentityUser user)
        {
            if (!await ConsumeRateAsync(user.Id)) return null;

            var now = DateTime.UtcNow;
            var rows = await _db.UserTokens.Where(t => t.Name == OtpName).ToListAsync();

            // drop expired codes (housekeeping) and collect the hashes that are still active
            var active = new HashSet<string>();
            foreach (var r in rows.ToList())
            {
                if (TryParse(r.Value, out var h, out var exp, out _) && exp > now) active.Add(h);
                else { _db.UserTokens.Remove(r); rows.Remove(r); }
            }

            string code, hash;
            var tries = 0;
            do
            {
                code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
                hash = Hash(code);
            } while (active.Contains(hash) && ++tries < 50);   // unique among all active codes (incl. the user's previous one)

            var value = $"{hash}|{(now + Lifetime).Ticks}|0";
            var row = await _db.UserTokens.FindAsync(user.Id, Provider, OtpName);
            if (row == null)
            {
                _db.UserTokens.Add(new IdentityUserToken<string> { UserId = user.Id, LoginProvider = Provider, Name = OtpName, Value = value });
            }
            else
            {
                row.Value = value;    // overwrites => the previous code is dead
            }
            await _db.SaveChangesAsync();
            return code;
        }

        public async Task<OtpResult> VerifyAsync(IdentityUser user, string? code)
        {
            var row = await _db.UserTokens.FindAsync(user.Id, Provider, OtpName);
            if (row == null || !TryParse(row.Value, out var storedHash, out var expires, out var attempts))
            {
                return OtpResult.Invalid;
            }
            if (expires <= DateTime.UtcNow)
            {
                _db.UserTokens.Remove(row);
                await _db.SaveChangesAsync();
                return OtpResult.Expired;
            }
            if (attempts >= MaxAttempts)
            {
                _db.UserTokens.Remove(row);
                await _db.SaveChangesAsync();
                return OtpResult.TooManyAttempts;
            }

            var supplied = Hash((code ?? string.Empty).Trim());
            var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied), Encoding.ASCII.GetBytes(storedHash));
            if (!ok)
            {
                attempts++;
                if (attempts >= MaxAttempts)
                {
                    _db.UserTokens.Remove(row);
                    await _db.SaveChangesAsync();
                    return OtpResult.TooManyAttempts;
                }
                row.Value = $"{storedHash}|{expires.Ticks}|{attempts}";
                await _db.SaveChangesAsync();
                return OtpResult.Invalid;
            }

            _db.UserTokens.Remove(row);          // single use
            await _db.SaveChangesAsync();
            return OtpResult.Ok;
        }

        /// <summary>Kills any active code (called after a successful password reset).</summary>
        public async Task InvalidateAsync(IdentityUser user)
        {
            var row = await _db.UserTokens.FindAsync(user.Id, Provider, OtpName);
            if (row != null)
            {
                _db.UserTokens.Remove(row);
                await _db.SaveChangesAsync();
            }
        }

        private async Task<bool> ConsumeRateAsync(string userId)
        {
            var now = DateTime.UtcNow;
            var row = await _db.UserTokens.FindAsync(userId, Provider, RateName);
            long start = now.Ticks; var count = 0;
            if (row?.Value != null)
            {
                var p = row.Value.Split('|');
                if (p.Length == 2 && long.TryParse(p[0], out var s) && int.TryParse(p[1], out var c) && now - new DateTime(s, DateTimeKind.Utc) < TimeSpan.FromHours(1))
                {
                    start = s; count = c;
                }
            }
            if (count >= MaxPerHour) return false;

            var value = $"{start}|{count + 1}";
            if (row == null) _db.UserTokens.Add(new IdentityUserToken<string> { UserId = userId, LoginProvider = Provider, Name = RateName, Value = value });
            else row.Value = value;
            await _db.SaveChangesAsync();
            return true;
        }

        private static bool TryParse(string? value, out string hash, out DateTime expires, out int attempts)
        {
            hash = string.Empty; expires = DateTime.MinValue; attempts = 0;
            if (string.IsNullOrEmpty(value)) return false;
            var p = value.Split('|');
            if (p.Length != 3 || !long.TryParse(p[1], out var ticks) || !int.TryParse(p[2], out attempts)) return false;
            hash = p[0];
            expires = new DateTime(ticks, DateTimeKind.Utc);
            return true;
        }
    }
}
