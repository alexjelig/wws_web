using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;

namespace wws_web.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string UserName { get; set; } = string.Empty;
        [BindProperty]
        public string Email { get; set; } = string.Empty;

        public IActionResult OnGet()
        {
            // Read session values
            UserName = HttpContext.Session.GetString("UserName") ?? string.Empty;
            Email = HttpContext.Session.GetString("Email") ?? string.Empty;

            // If not logged in, redirect immediately to the Login page
            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Email))
            {
                return RedirectToPage("/Login");
            }

            // Otherwise render the page as usual
            return Page();
        }

        // Keep your logout handler here (if you use it from the layout)
        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Login");
        }
    }
}
