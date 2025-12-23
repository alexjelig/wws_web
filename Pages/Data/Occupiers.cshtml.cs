using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Diagnostics;
using wws_web.Services;

namespace wws_web.Pages.Data
{
    public class OccupiersModel : PageModel
    {
        private readonly DeviceManager _devices;
        private readonly ScannerDevice _scanner;
        private readonly ILogger<OccupiersModel> _logger;
        private readonly string _dbPath = @"C:\wws\wws.db3";

        public List<Occupier> Occupiers { get; set; } = new List<Occupier>();

        [BindProperty]
        public string? OccTag { get; set; }

        [BindProperty]
        public string? OccName { get; set; }

        [BindProperty]
        public bool IsEditMode { get; set; }

        [BindProperty]
        public long EditId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1; // Added this property to resolve the missing symbol error

        public bool HasNextPage { get; set; } // Added this property to support pagination logic

        public OccupiersModel(DeviceManager devices, ILogger<OccupiersModel> logger)
        {
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Try to resolve the named scanner device from the DeviceManager.
            // The DeviceManager.Get<T> returns T or null; adapt if your real implementation is different.
            _scanner = _devices.Get<ScannerDevice>("Scanner")
                ?? throw new InvalidOperationException("ScannerDevice named 'Scanner' not registered in DeviceManager.");
        }

        public void OnGet()
        {
            // Acquire scanner so port opens while on this page
            try
            {
                if (_scanner != null)
                {
                    _scanner.Acquire();
                    _logger.LogDebug("[Occupiers] Scanner acquired.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Occupiers] Failed to acquire scanner.");
            }

            const int PageSize = 10;

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT rowid, occTag, occName FROM occupiers LIMIT @limit OFFSET @offset";
                    command.Parameters.AddWithValue("@limit", PageSize);
                    command.Parameters.AddWithValue("@offset", (PageNumber - 1) * PageSize);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = reader.GetInt64(0);
                            var tag = reader.GetString(1);
                            var name = reader.GetString(2);

                            Occupiers.Add(new Occupier { Id = id, OccTag = tag, OccName = name });
                        }

                        HasNextPage = Occupiers.Count == PageSize;
                    }
                }
            }
        }

        // Add Release handler for client sendBeacon on unload
        [IgnoreAntiforgeryToken]
        public JsonResult OnPostRelease()
        {
            try
            {
                if (_scanner != null)
                {
                    _scanner.Release();
                    _logger.LogDebug("[Occupiers] Scanner released from client.");
                    return new JsonResult(new { ok = true });
                }
                return new JsonResult(new { ok = false, error = "Scanner not available" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Occupiers] OnPostRelease error");
                return new JsonResult(new { ok = false, error = ex.Message });
            }
        }

        public JsonResult OnGetLatestTag()
        {
            try
            {
                _logger.LogDebug("[OnGetLatestTag] called");

                if (_scanner == null)
                {
                    _logger.LogWarning("[OnGetLatestTag] ScannerDevice is null");
                    return new JsonResult(new { tag = "", exists = false, error = "scanner null" });
                }

                string tag = _scanner.ConsumeLatest();
                _logger.LogDebug("[OnGetLatestTag] ConsumeLatest -> '{Tag}'", tag ?? "(empty)");

                if (string.IsNullOrWhiteSpace(tag))
                {
                    return new JsonResult(new { tag = "", exists = false });
                }

                // optional DB lookup
                string occName = "";
                using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT occName FROM occupiers WHERE occTag = @tag LIMIT 1";
                        command.Parameters.AddWithValue("@tag", tag);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                occName = reader.GetString(0);
                            }
                        }
                    }
                }

                _logger.LogDebug("[OnGetLatestTag] returning tag='{Tag}' occName='{Name}'", tag, occName);
                return new JsonResult(new { tag, occName, exists = !string.IsNullOrEmpty(occName) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OnGetLatestTag] exception");
                return new JsonResult(new { tag = "", exists = false, error = ex.Message });
            }
        }

        public void OnGetEdit(long id)
        {
            // Load the occupier to edit
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT occTag, occName FROM occupiers WHERE rowid = @id";
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            OccTag = reader.GetString(0);
                            OccName = reader.GetString(1);
                            EditId = id;
                            IsEditMode = true;
                        }
                    }
                }
            }

            // Also load the list so the table displays
            const int PageSize = 10;
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT rowid, occTag, occName FROM occupiers LIMIT @limit OFFSET @offset";
                    command.Parameters.AddWithValue("@limit", PageSize);
                    command.Parameters.AddWithValue("@offset", (PageNumber - 1) * PageSize);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id2 = reader.GetInt64(0);
                            var tag = reader.GetString(1);
                            var name = reader.GetString(2);

                            Occupiers.Add(new Occupier { Id = id2, OccTag = tag, OccName = name });
                        }

                        HasNextPage = Occupiers.Count == PageSize;
                    }
                }
            }
        }

        public IActionResult OnPostAdd()
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO occupiers (occTag, occName) VALUES (@occTag, @occName)";
                    command.Parameters.AddWithValue("@occTag", OccTag);
                    command.Parameters.AddWithValue("@occName", OccName);
                    command.ExecuteNonQuery();
                }
            }

            return RedirectToPage();
        }

        public IActionResult OnPostSave()
        {
            if (EditId <= 0)
            {
                ModelState.AddModelError(string.Empty, "No valid record selected for editing.");
                return Page();
            }

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE occupiers SET occTag = @occTag, occName = @occName WHERE rowid = @id";
                    command.Parameters.AddWithValue("@occTag", OccTag);
                    command.Parameters.AddWithValue("@occName", OccName);
                    command.Parameters.AddWithValue("@id", EditId);

                    if (command.ExecuteNonQuery() == 0)
                    {
                        ModelState.AddModelError(string.Empty, "Failed to update the record.");
                        return Page();
                    }
                }
            }

            IsEditMode = false; // Reset mode to Add
            EditId = 0; // Reset tracking ID
            return RedirectToPage();
        }

        public IActionResult OnPostDelete(long id)
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM occupiers WHERE rowid = @id";
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }

            return RedirectToPage();
        }
    }

    public class Occupier
    {
        public long Id { get; set; }
        public string? OccTag { get; set; }
        public string? OccName { get; set; }
    }
}
