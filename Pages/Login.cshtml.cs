using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using wws_web.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace wws_web.Pages
{
    public class LoginModel : PageModel
    {
        private readonly LoginService _loginService;

        public LoginModel(LoginService loginService)
        {
            _loginService = loginService;
        }

        [BindProperty]
        public string UserName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;
        [BindProperty]
        public string Password { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter username, email, and password.";
                return Page();
            }

            // Get user by username AND email
            var user = await _loginService.GetUserByUserNameEmailAndPasswordAsync(UserName, Email);

            if (user == null || !user.IsActive)
            {
                ErrorMessage = "Invalid username, email, or password.";
                return Page();
            }

            // Check password
            if (PasswordHasher.VerifyPassword(Password, user.PasswordHash))
            {
                await _loginService.UpdateLoginSuccessAsync(user.UserID);
                HttpContext.Session.SetString("UserName", user.UserName ?? "");
                HttpContext.Session.SetString("Email", user.Email ?? "");
                return RedirectToPage("/Index");
            }
            else
            {
                await _loginService.UpdateLoginFailAsync(user.UserID);
                ErrorMessage = "Invalid username, email, or password.";
                return Page();
            }
        }
    }
}
