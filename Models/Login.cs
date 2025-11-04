using System;

namespace wws_web.Models
{
    public class Login
    {
        public int UserID { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string AppMod { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime? LastLogin { get; set; }
        public int AmountOfLogins { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public string? PasswordResetToken { get; set; } = string.Empty;
        public DateTime? PasswordResetExpires { get; set; }
        public DateTime? LastFailedLogin { get; set; }
        public int FailedLoginCount { get; set; }
        public string? ProfileData { get; set; } = string.Empty;
    }
}
