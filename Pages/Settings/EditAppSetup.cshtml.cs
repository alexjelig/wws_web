using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using wws_web.Models;
using wws_web.Services;

namespace wws_web.Pages.Settings
{
    public class EditAppSetupModel : PageModel
    {
        private readonly IFileSettingsService _fileSettings;
        private readonly IConfiguration _config;

        public EditAppSetupModel(IFileSettingsService fileSettings, IConfiguration config)
        {
            _fileSettings = fileSettings;
            _config = config;
        }

        [BindProperty]
        public AppSetup Model { get; set; } = new AppSetup();

        // Default base directory for settings files. Use verbatim string to avoid escape issues.
        private string DefaultFilePath
        {
            get
            {
                var configured = _config.GetValue<string>("SettingsBasePath")?.Trim();
                var basePath = string.IsNullOrWhiteSpace(configured) ? @"C:\wws" : configured;

                // Normalize to full path
                var full = Path.GetFullPath(basePath);

                // Ensure directory exists (create on demand)
                if (!Directory.Exists(full))
                    Directory.CreateDirectory(full);

                return full;
            }
        }

        // Path to the default appsetup.json inside the settings base folder
        private string DefaultAppSetupPath => Path.Combine(DefaultFilePath, "appsetup.json");

        // Helper: ensure the provided path is inside the SettingsBasePath
        // Returns the normalized absolute path or throws ArgumentException if invalid
        private string NormalizeAndValidatePath(string? incomingPath)
        {
            string resultPath;
            if (string.IsNullOrWhiteSpace(incomingPath))
            {
                resultPath = DefaultAppSetupPath;
            }
            else
            {
                // If user provided a relative path, treat it as relative to the DefaultFilePath
                if (!Path.IsPathRooted(incomingPath))
                {
                    resultPath = Path.Combine(DefaultFilePath, incomingPath);
                }
                else
                {
                    resultPath = incomingPath;
                }
            }

            resultPath = Path.GetFullPath(resultPath);

            // Ensure the target path is within the allowed folder (prevent directory traversal)
            var allowedPrefix = DefaultFilePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
            if (!resultPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(resultPath, DefaultFilePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The provided file path is outside of the allowed settings folder.");
            }

            return resultPath;
        }

        public async Task<IActionResult> OnGetAsync(string? filePath)
        {
            try
            {
                var path = NormalizeAndValidatePath(filePath);
                var loaded = await _fileSettings.ReadAsync<AppSetup>(path);
                if (loaded != null)
                    Model = loaded;
                // else leave Model defaults

                ViewData["SettingsFilePath"] = path;
                return Page();
            }
            catch (ArgumentException)
            {
                // invalid path supplied
                ModelState.AddModelError(string.Empty, "Invalid settings file path.");
                ViewData["SettingsFilePath"] = DefaultAppSetupPath;
                return Page();
            }
            catch (Exception ex)
            {
                // log the exception in real app; show a friendly message here
                ModelState.AddModelError(string.Empty, "Could not read settings: " + ex.Message);
                ViewData["SettingsFilePath"] = DefaultAppSetupPath;
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync(string? filePath)
        {
            if (!ModelState.IsValid)
            {
                ViewData["SettingsFilePath"] = filePath ?? DefaultAppSetupPath;
                return Page();
            }

            try
            {
                var path = NormalizeAndValidatePath(filePath);
                await _fileSettings.WriteAsync(path, Model);
                TempData["Message"] = "Saved successfully to " + path;

                // Redirect to GET to avoid double-post; use absolute path so resolution is reliable
                return RedirectToPage("/Settings/EditAppSetup", new { filePath = path });
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "Invalid settings file path.");
                ViewData["SettingsFilePath"] = DefaultAppSetupPath;
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Could not save settings: " + ex.Message);
                ViewData["SettingsFilePath"] = filePath ?? DefaultAppSetupPath;
                return Page();
            }
        }
    }
}
