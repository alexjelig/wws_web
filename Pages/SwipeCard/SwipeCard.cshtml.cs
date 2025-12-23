using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using wws_web.Services;

namespace wws_web.Pages.SwipeCard
{
    public class SwipeCardModel : PageModel
    {
        private readonly DeviceManager _devices;
        private readonly string _dbPath = @"C:\wws\wws.db3";

        public SwipeCardModel(DeviceManager devices)
        {
            _devices = devices;
        }

        // Current selection shown on the page (loaded from session)
        public string? SelectedOccTag { get; private set; }
        public string? SelectedOccName { get; private set; }

        // Basic status for diagnostics
        public bool ScannerIsOpen { get; private set; }
        public int ScannerRefCount { get; private set; }

        public void OnGet()
        {
            // Load any existing selection from session so the UI can show it
            SelectedOccTag = HttpContext.Session.GetString("SelectedOccTag") ?? string.Empty;
            SelectedOccName = HttpContext.Session.GetString("SelectedOccName") ?? string.Empty;

            // Acquire scanner (open port) for the duration of the user's visit to this page.
            try
            {
                var scanner = _devices.Get<ScannerDevice>("Scanner");
                if (scanner != null)
                {
                    // Acquire increments refcount and opens port when needed
                    scanner.Acquire();
                    ScannerIsOpen = scanner.IsOpen;
                    // If you exposed CurrentRefCount on ScannerDevice, read it; otherwise skip.
                    try
                    {
                        var prop = scanner.GetType().GetProperty("CurrentRefCount");
                        if (prop != null)
                        {
                            ScannerRefCount = (int)(prop.GetValue(scanner) ?? 0);
                        }
                    }
                    catch { }
                    Debug.WriteLine("[SwipeCard] Scanner acquired on OnGet. IsOpen=" + ScannerIsOpen);
                }
                else
                {
                    Debug.WriteLine("[SwipeCard] Scanner not found in DeviceManager.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SwipeCard] Failed to acquire scanner: " + ex.Message);
            }
        }

        // Polling endpoint: returns the latest scanned tag (consumes it) and occupant lookup result
        public JsonResult OnGetLatestTag()
        {
            try
            {
                var scanner = _devices.Get<ScannerDevice>("Scanner");
                if (scanner == null)
                {
                    return new JsonResult(new { ok = false, tag = "", name = "", found = false, error = "Scanner not available" });
                }

                // ConsumeLatest returns empty string when no new tag
                var tag = scanner.ConsumeLatest() ?? "";
                if (string.IsNullOrWhiteSpace(tag))
                {
                    return new JsonResult(new { ok = true, tag = "", name = "", found = false });
                }

                // Lookup in occupiers table
                string foundName = "";
                bool found = false;
                try
                {
                    using var conn = new SqliteConnection($"Data Source={_dbPath}");
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT occName FROM occupiers WHERE occTag = @tag LIMIT 1";
                    cmd.Parameters.AddWithValue("@tag", tag);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        foundName = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        found = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[SwipeCard] Database lookup error: " + ex.Message);
                    // Return tag but indicate error via error field
                    // Also store tag in session even if DB failed so user can see it
                    HttpContext.Session.SetString("SelectedOccTag", tag);
                    HttpContext.Session.SetString("SelectedOccName", "");
                    return new JsonResult(new { ok = false, tag = tag, name = "", found = false, error = ex.Message });
                }

                // Save into session for later use (Reports saving etc.)
                HttpContext.Session.SetString("SelectedOccTag", tag);
                HttpContext.Session.SetString("SelectedOccName", foundName ?? "");

                Debug.WriteLine($"[SwipeCard] Tag scanned: {tag}, found={found}, name={foundName}");

                return new JsonResult(new { ok = true, tag = tag, name = foundName ?? "", found = found });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SwipeCard] OnGetLatestTag error: " + ex.Message);
                return new JsonResult(new { ok = false, tag = "", name = "", found = false, error = ex.Message });
            }
        }

        // Release handler: called by client on unload via sendBeacon (best-effort)
        [IgnoreAntiforgeryToken]
        public JsonResult OnPostRelease()
        {
            try
            {
                var scanner = _devices.Get<ScannerDevice>("Scanner");
                if (scanner != null)
                {
                    scanner.Release();
                    Debug.WriteLine("[SwipeCard] Scanner Release called from client.");
                    return new JsonResult(new { ok = true });
                }

                return new JsonResult(new { ok = false, error = "Scanner not found" });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SwipeCard] OnPostRelease error: " + ex.Message);
                return new JsonResult(new { ok = false, error = ex.Message });
            }
        }

        // Optional: clear current selection from session (form post)
        [ValidateAntiForgeryToken]
        public IActionResult OnPostClear()
        {
            HttpContext.Session.Remove("SelectedOccTag");
            HttpContext.Session.Remove("SelectedOccName");
            return RedirectToPage(); // reload
        }
    }
}
