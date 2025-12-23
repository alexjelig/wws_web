using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.IO;
using wws_web.Models;
using wws_web.Data;

namespace wws_web.Pages.Data.Waste
{
    public class CreateModel : PageModel
    {
        private readonly SqliteDbHandler _db;
        private readonly IWebHostEnvironment _env;

        public CreateModel(SqliteDbHandler db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [BindProperty]
        public WasteItem Item { get; set; } = new WasteItem();

        public void OnGet()
        {
        }

        public IActionResult OnPost(IFormFile PictureFile)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // If a file is uploaded, save it to wwwroot/images/waste and set Item.WastePicture to the web path
            if (PictureFile != null && PictureFile.Length > 0)
            {
                var ext = Path.GetExtension(PictureFile.FileName);
                var safeName = $"{Guid.NewGuid():N}{ext}";
                var imagesDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "waste");
                if (!Directory.Exists(imagesDir)) Directory.CreateDirectory(imagesDir);
                var dest = Path.Combine(imagesDir, safeName);
                using (var fs = new FileStream(dest, FileMode.Create))
                {
                    PictureFile.CopyTo(fs);
                }
                Item.WastePicture = "/images/waste/" + safeName;
            }

            _db.AddWaste(Item);

            // After save, return to list
            return RedirectToPage("./Index");
        }
    }
}
