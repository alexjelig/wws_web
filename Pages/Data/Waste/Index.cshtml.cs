using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.IO;
using wws_web.Data;
using wws_web.Models;

namespace wws_web.Pages.Data.Waste
{
    public class IndexModel : PageModel
    {
        private readonly SqliteDbHandler _db;
        private readonly IWebHostEnvironment _env;

        public IndexModel(SqliteDbHandler db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public class DisplayItem : WasteItem
        {
            public string? DisplayImageUrl { get; set; }
        }

        public List<DisplayItem> Items { get; set; } = new List<DisplayItem>();

        public void OnGet()
        {
            Items.Clear();
            var all = _db.GetAllWaste();
            foreach (var it in all)
            {
                var disp = new DisplayItem
                {
                    Id = it.Id,
                    WasteType = it.WasteType,
                    WastePicture = it.WastePicture
                };

                if (!string.IsNullOrEmpty(it.WastePicture) && it.WastePicture.StartsWith("/"))
                {
                    disp.DisplayImageUrl = it.WastePicture;
                }
                else if (!string.IsNullOrEmpty(it.WastePicture))
                {
                    var fileName = Path.GetFileName(it.WastePicture);
                    var webPathPhysical = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "waste", fileName);
                    if (System.IO.File.Exists(webPathPhysical))
                    {
                        disp.DisplayImageUrl = "/images/waste/" + fileName;
                    }
                    else
                    {
                        disp.DisplayImageUrl = null;
                    }
                }
                else
                {
                    disp.DisplayImageUrl = null;
                }

                Items.Add(disp);
            }
        }

    }
}
