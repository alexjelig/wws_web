using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using wws_web.Models;

namespace wws_web.Pages.Selection
{
    public class SelectionModel : PageModel
    {
        private readonly string _dbPath = @"C:\wws\wws.db3";
        private const int PageSize = 9;

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public List<WasteItem> Wastes { get; set; } = new List<WasteItem>();
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }

        public int? SelectedWasteId { get; set; }
        public string? SelectedWasteType { get; set; }

        public void OnGet()
        {
            Debug.WriteLine("[IndexModel] OnGet called, PageNumber={0}", PageNumber);
            LoadSelectedFromSession();
            LoadWastes();
        }

        private void LoadWastes()
        {
            Wastes.Clear();

            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                Debug.WriteLine("[IndexModel] Database connection opened: {0}", _dbPath);

                using var cmd = conn.CreateCommand();
                // Query the correct table: waste (singular), with columns: id, wasteType, wastePicture
                cmd.CommandText = @"SELECT id, wasteType, wastePicture
                                    FROM waste
                                    ORDER BY id
                                    LIMIT @limit OFFSET @offset";
                cmd.Parameters.AddWithValue("@limit", PageSize);
                cmd.Parameters.AddWithValue("@offset", (PageNumber - 1) * PageSize);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var waste = new WasteItem
                    {
                        Id = reader.GetInt32(0),
                        WasteType = reader.GetString(1),
                        WastePicture = reader.IsDBNull(2) ? "" : reader.GetString(2)
                    };
                    Wastes.Add(waste);
                    Debug.WriteLine("[IndexModel] Loaded waste: Id={0}, Type={1}, Picture={2}", waste.Id, waste.WasteType, waste.WastePicture);
                }

                // Determine pagination
                using var countCmd = conn.CreateCommand();
                countCmd.CommandText = @"SELECT COUNT(*) FROM waste";
                var total = Convert.ToInt32(countCmd.ExecuteScalar());
                Debug.WriteLine("[IndexModel] Total waste items in DB: {0}", total);

                HasPreviousPage = PageNumber > 1;
                HasNextPage = PageNumber * PageSize < total;

                Debug.WriteLine("[IndexModel] Loaded {0} items for page {1}. HasPreviousPage={2}, HasNextPage={3}", Wastes.Count, PageNumber, HasPreviousPage, HasNextPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IndexModel] LoadWastes error: {ex.Message}");
            }
        }

        private void LoadSelectedFromSession()
        {
            SelectedWasteId = HttpContext.Session.GetInt32("SelectedWasteId");
            SelectedWasteType = HttpContext.Session.GetString("SelectedWasteType");
            Debug.WriteLine("[IndexModel] Loaded from session: SelectedWasteId={0}, SelectedWasteType={1}", SelectedWasteId, SelectedWasteType);
        }

        [ValidateAntiForgeryToken]
        public JsonResult OnPostSelectWaste([FromBody] WasteSelectionDto dto)
        {
            try
            {
                if (dto == null)
                {
                    Debug.WriteLine("[IndexModel] OnPostSelectWaste: dto is null");
                    return new JsonResult(new { ok = false, message = "No selection payload." });
                }

                Debug.WriteLine("[IndexModel] OnPostSelectWaste: Id={0}, WasteType={1}", dto.Id, dto.WasteType);

                // Store in session: id and wasteType only (picture not needed for reports)
                HttpContext.Session.SetInt32("SelectedWasteId", dto.Id);
                HttpContext.Session.SetString("SelectedWasteType", dto.WasteType ?? "");

                Debug.WriteLine("[IndexModel] Selection saved to session.");

                return new JsonResult(new
                {
                    ok = true,
                    id = dto.Id,
                    type = dto.WasteType
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IndexModel] OnPostSelectWaste error: {ex.Message}");
                return new JsonResult(new { ok = false, message = ex.Message });
            }
        }

        public JsonResult OnGetSelectedStatus()
        {
            LoadSelectedFromSession();
            return new JsonResult(new
            {
                id = SelectedWasteId,
                type = SelectedWasteType
            });
        }

        // DTO used for AJAX selection POST
        public class WasteSelectionDto
        {
            public int Id { get; set; }
            public string? WasteType { get; set; }
        }
    }
}
