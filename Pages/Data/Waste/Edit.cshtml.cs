using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using wws_web.Data;
using wws_web.Models;

namespace wws_web.Pages.Data.Waste
{
    public class EditModel : PageModel
    {
        private readonly SqliteDbHandler _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EditModel> _logger;

        public EditModel(SqliteDbHandler db, IWebHostEnvironment env, ILogger<EditModel> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        // Bound property holds posted fields (Id and WasteType).
        [BindProperty]
        public WasteItem Item { get; set; } = new WasteItem();

        // Populate the page for GET
        public IActionResult OnGet(int id)
        {
            try
            {
                _logger.LogInformation("Edit OnGet called for id={Id}", id);
                Debug.WriteLine($"[Edit.OnGet] id={id}");

                var fetched = _db.GetWasteById(id);
                if (fetched == null)
                {
                    _logger.LogWarning("Edit OnGet: item not found id={Id}", id);
                    Debug.WriteLine($"[Edit.OnGet] item not found id={id}");
                    return RedirectToPage("./Index");
                }

                Item = fetched;
                _logger.LogDebug("Edit OnGet: loaded item {@Item}", Item);
                Debug.WriteLine($"[Edit.OnGet] loaded item Id={Item.Id}, WasteType='{Item.WasteType}', WastePicture='{Item.WastePicture}'");
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Edit.OnGet for id={Id}", id);
                Debug.WriteLine($"[Edit.OnGet] exception: {ex}");
                return RedirectToPage("./Index");
            }
        }

        // Robust POST handler: fetch existing row, update fields, preserve picture when no file uploaded.
        public IActionResult OnPost(IFormFile PictureFile)
        {
            try
            {
                _logger.LogInformation("Edit OnPost called");
                Debug.WriteLine("[Edit.OnPost] called");

                // Log posted Item fields
                Debug.WriteLine($"[Edit.OnPost] Bound Item.Id = {Item?.Id}");
                Debug.WriteLine($"[Edit.OnPost] Bound Item.WasteType = '{Item?.WasteType}'");

                // If Id not bound, try fallback from Request.Form
                if (Item?.Id <= 0)
                {
                    var formId = Request.Form["Item.Id"].ToString();
                    Debug.WriteLine($"[Edit.OnPost] fallback form Item.Id = '{formId}'");
                    if (int.TryParse(formId, out var fallbackId))
                    {
                        Item.Id = fallbackId;
                        Debug.WriteLine($"[Edit.OnPost] parsed fallback id = {fallbackId}");
                    }
                }

                if (Item?.Id <= 0)
                {
                    _logger.LogWarning("Edit OnPost: invalid or missing Item.Id. Form keys: {Keys}", Request.Form.Keys);
                    Debug.WriteLine("[Edit.OnPost] invalid or missing Item.Id. Aborting and redirecting to Index.");
                    return RedirectToPage("./Index");
                }

                // If there is no uploaded file, remove any ModelState errors related to PictureFile
                // (some model binders or client validation can add a "required" error for file inputs)
                var hasFileInRequest = Request.Form.Files?.Count > 0;
                Debug.WriteLine($"[Edit.OnPost] Request.Form.Files count = {Request.Form.Files?.Count}");
                if (!hasFileInRequest)
                {
                    if (ModelState.ContainsKey("PictureFile"))
                    {
                        Debug.WriteLine("[Edit.OnPost] No file uploaded -> removing ModelState entry for 'PictureFile'");
                        ModelState.Remove("PictureFile");
                    }
                    // also handle possible keys like "Item.PictureFile" just in case
                    if (ModelState.ContainsKey("Item.PictureFile"))
                    {
                        Debug.WriteLine("[Edit.OnPost] No file uploaded -> removing ModelState entry for 'Item.PictureFile'");
                        ModelState.Remove("Item.PictureFile");
                    }
                }

                // Log ModelState info
                _logger.LogDebug("Edit OnPost: ModelState.IsValid = {IsValid}", ModelState.IsValid);
                Debug.WriteLine($"[Edit.OnPost] ModelState.IsValid = {ModelState.IsValid}");
                if (!ModelState.IsValid)
                {
                    Debug.WriteLine("[Edit.OnPost] ModelState errors:");
                    foreach (var kv in ModelState)
                    {
                        if (kv.Value.Errors.Count > 0)
                        {
                            foreach (var err in kv.Value.Errors)
                            {
                                Debug.WriteLine($"  [ModelState] Key='{kv.Key}', Error='{err.ErrorMessage}'");
                                _logger.LogWarning("ModelState error Key={Key} Error={Error}", kv.Key, err.ErrorMessage);
                            }
                        }
                    }
                }

                // Load existing record from DB
                var existing = _db.GetWasteById(Item.Id);
                if (existing == null)
                {
                    _logger.LogWarning("Edit OnPost: existing item not found for Id={Id}", Item.Id);
                    Debug.WriteLine($"[Edit.OnPost] existing not found for Id={Item.Id}. Redirecting to Index.");
                    return RedirectToPage("./Index");
                }

                Debug.WriteLine($"[Edit.OnPost] existing before update: Id={existing.Id}, WasteType='{existing.WasteType}', WastePicture='{existing.WastePicture}'");
                _logger.LogDebug("Edit OnPost: existing before update {@Existing}", existing);

                // If ModelState invalid, preserve picture and redisplay
                if (!ModelState.IsValid)
                {
                    Item.WastePicture = existing.WastePicture;
                    Debug.WriteLine("[Edit.OnPost] ModelState invalid; returning Page() with preserved picture");
                    return Page();
                }

                // Update allowed fields
                existing.WasteType = Item.WasteType ?? existing.WasteType;
                Debug.WriteLine($"[Edit.OnPost] Will set WasteType='{existing.WasteType}'");

                // Log PictureFile state
                if (PictureFile != null)
                {
                    Debug.WriteLine($"[Edit.OnPost] PictureFile provided: name='{PictureFile.FileName}', length={PictureFile.Length}");
                    _logger.LogDebug("Edit OnPost: PictureFile provided name={Name} length={Length}", PictureFile.FileName, PictureFile.Length);
                }
                else
                {
                    Debug.WriteLine("[Edit.OnPost] PictureFile is null (no file uploaded)");
                    _logger.LogDebug("Edit OnPost: PictureFile is null");
                }

                // If a new picture was uploaded, save it and update the path; otherwise preserve the old path
                if (PictureFile != null && PictureFile.Length > 0)
                {
                    var ext = Path.GetExtension(PictureFile.FileName);
                    var safeName = $"{Guid.NewGuid():N}{ext}";
                    var imagesDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "waste");

                    Debug.WriteLine($"[Edit.OnPost] Saving uploaded image to imagesDir='{imagesDir}' destName='{safeName}'");
                    _logger.LogDebug("Edit OnPost: saving uploaded image to {DestDir}\\{File}", imagesDir, safeName);

                    if (!Directory.Exists(imagesDir))
                    {
                        Debug.WriteLine($"[Edit.OnPost] images dir does not exist, creating '{imagesDir}'");
                        Directory.CreateDirectory(imagesDir);
                    }

                    var dest = Path.Combine(imagesDir, safeName);
                    try
                    {
                        using (var fs = new FileStream(dest, FileMode.Create))
                        {
                            PictureFile.CopyTo(fs);
                        }
                        existing.WastePicture = "/images/waste/" + safeName;
                        Debug.WriteLine($"[Edit.OnPost] Saved file to '{dest}', updated WastePicture='{existing.WastePicture}'");
                        _logger.LogInformation("Edit OnPost: saved uploaded image to {Dest}", dest);
                    }
                    catch (Exception fsEx)
                    {
                        _logger.LogError(fsEx, "Failed to save uploaded image to {Dest}", dest);
                        Debug.WriteLine($"[Edit.OnPost] exception while saving file: {fsEx}");
                        // Preserve existing picture on failure
                        existing.WastePicture = existing.WastePicture;
                    }
                }
                else
                {
                    // No new file: preserve the existing path explicitly
                    existing.WastePicture = existing.WastePicture;
                    Debug.WriteLine("[Edit.OnPost] No new image uploaded, preserving existing WastePicture.");
                }

                // Persist update
                _db.UpdateWaste(existing);
                Debug.WriteLine($"[Edit.OnPost] Database updated for Id={existing.Id}. WasteType='{existing.WasteType}', WastePicture='{existing.WastePicture}'");
                _logger.LogInformation("Edit OnPost: updated DB for id={Id}", existing.Id);

                // After successful save, redirect back to the list
                Debug.WriteLine("[Edit.OnPost] Redirecting to Index");
                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in Edit.OnPost for Item.Id={Id}", Item?.Id);
                Debug.WriteLine($"[Edit.OnPost] unhandled exception: {ex}");
                // Repopulate Item from DB to keep preview intact if possible
                try
                {
                    if (Item?.Id > 0)
                    {
                        var fallback = _db.GetWasteById(Item.Id);
                        if (fallback != null)
                        {
                            Item.WastePicture = fallback.WastePicture;
                        }
                    }
                }
                catch { /* ignore */ }

                ModelState.AddModelError("", "An unexpected error occurred. Check logs for details.");
                return Page();
            }
        }
    }
}
