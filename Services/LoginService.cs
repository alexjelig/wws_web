using System;
using System.Threading.Tasks;
using System.Data;
using MySqlConnector;
using Microsoft.Extensions.Configuration;
using wws_web.Models;

namespace wws_web.Services
{
    public class LoginService
    {
        private readonly string _connectionString;

        public LoginService(IConfiguration configuration)
        {
            // Ensure _connectionString is never null. Throw a clear exception if the configuration is missing.
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Configuration error: connection string 'DefaultConnection' is not set. Please add it to appsettings.json or environment variables.");
        }

        // (partial file — only the updated method shown)
        public async Task<Login?> GetUserByUserNameEmailAndPasswordAsync(string userName, string email)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand("SELECT * FROM login WHERE UserName = @UserName AND Email = @Email LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@UserName", userName ?? "");
            cmd.Parameters.AddWithValue("@Email", email ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            bool found = await reader.ReadAsync();   // <-- read once and store result

            if (!found)
            {
                return null;
            }

            // Map fields (keep nullable handling as before)
            var user = new Login
            {
                UserID = reader.GetInt32("UserID"),
                AppName = reader.GetString("AppName"),
                AppMod = reader.GetString("AppMod"),
                UserName = reader.GetString("UserName"),
                Email = reader.GetString("Email"),
                PasswordHash = reader.GetString("PasswordHash"),
                LastLogin = reader.IsDBNull("LastLogin") ? (DateTime?)null : reader.GetDateTime("LastLogin"),
                AmountOfLogins = reader.GetInt32("AmountOfLogins"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                IsActive = reader.GetBoolean("IsActive"),
                Role = reader.GetString("Role"),
                EmailVerified = reader.GetBoolean("EmailVerified"),
                PasswordResetToken = reader.IsDBNull("PasswordResetToken") ? null : reader.GetString("PasswordResetToken"),
                PasswordResetExpires = reader.IsDBNull("PasswordResetExpires") ? (DateTime?)null : reader.GetDateTime("PasswordResetExpires"),
                LastFailedLogin = reader.IsDBNull("LastFailedLogin") ? (DateTime?)null : reader.GetDateTime("LastFailedLogin"),
                FailedLoginCount = reader.GetInt32("FailedLoginCount"),
                ProfileData = reader.IsDBNull("ProfileData") ? null : reader.GetString("ProfileData")
            };

            return user;
        }

        public async Task UpdateLoginSuccessAsync(int userId)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(@"
                UPDATE login
                SET LastLogin = NOW(),
                    AmountOfLogins = AmountOfLogins + 1,
                    FailedLoginCount = 0
                WHERE UserID = @UserID", conn);
            cmd.Parameters.AddWithValue("@UserID", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateLoginFailAsync(int userId)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(@"
                UPDATE login
                SET LastFailedLogin = NOW(),
                    FailedLoginCount = FailedLoginCount + 1
                WHERE UserID = @UserID", conn);
            cmd.Parameters.AddWithValue("@UserID", userId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
