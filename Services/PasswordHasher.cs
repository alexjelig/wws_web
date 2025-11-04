using System;
using System.Text;
using System.Security.Cryptography;
using Konscious.Security.Cryptography;

namespace wws_web.Services
{
    public static class PasswordHasher
    {
        // Verifies the password against a hash in "salt:hash" (Base64) format
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            var parts = storedHash.Split(':');
            if (parts.Length != 2)
                return false;

            var salt = Convert.FromBase64String(parts[0]);
            var hashBytes = Convert.FromBase64String(parts[1]);

            using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
            {
                argon2.Salt = salt;
                argon2.DegreeOfParallelism = 2;
                argon2.MemorySize = 65536;
                argon2.Iterations = 4;

                var computedHash = argon2.GetBytes(32); // 32 bytes output, same as your PowerShell

                return CryptographicOperations.FixedTimeEquals(computedHash, hashBytes);
            }
        }
    }
}
