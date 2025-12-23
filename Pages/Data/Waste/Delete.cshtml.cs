using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using wws_web.Models;
using wws_web.Data;

namespace wws_web.Pages.Data.Waste
{
    public class DeleteModel : PageModel
    {
        private readonly SqliteDbHandler _db;

        public DeleteModel(SqliteDbHandler db)
        {
            _db = db;
        }

        [BindProperty]
        public WasteItem Item { get; set; } = new WasteItem(); // initialized to avoid CS8618

        public IActionResult OnGet(int id)
        {
            var fetched = _db.GetWasteById(id);
            if (fetched == null) return RedirectToPage("./Index");
            Item = fetched;
            return Page();
        }

        public IActionResult OnPost()
        {
            if (Item?.Id > 0)
            {
                _db.DeleteWaste(Item.Id);
            }
            return RedirectToPage("./Index");
        }
    }
}
