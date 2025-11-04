using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using wws_web.Models;
using wws_web.Services;

namespace wws_web.Pages.Settings
{
    public class EditWeightModel : PageModel
    {
        private readonly IFileSettingsService _fileSettings;
        private readonly IConfiguration _config;

        public EditWeightModel(IFileSettingsService fileSettings, IConfiguration config)
        {
            _fileSettings = fileSettings;
            _config = config;
        }

        [BindProperty]
        public WeightSettings Model { get; set; } = new WeightSettings();

        // UI helpers (same lists as scanner page)
        public List<string> AvailablePorts { get; set; } = new List<string>();
        public List<int> CommonBaudRates { get; set; } = new List<int> { 300, 1200, 2400, 4800, 9600, 14400, 19200, 38400, 57600, 115200 };
        public List<int> DataBitsOptions { get; set; } = new List<int> { 5, 6, 7, 8 };
        public List<string> ParityOptions { get; set; } = Enum.GetNames(typeof(Parity)).ToList();
        public List<string> StopBitsOptions { get; set; } = Enum.GetNames(typeof(StopBits)).ToList();

        // Default base directory for settings files.
        private string DefaultFilePath
        {
            get
            {
                var configured = _config.GetValue<string>("SettingsBasePath")?.Trim();
                var basePath = string.IsNullOrWhiteSpace(configured) ? @"C:\wws" : configured;
                var full = Path.GetFullPath(basePath);
                if (!Directory.Exists(full))
                    Directory.CreateDirectory(full);
                return full;
            }
        }

        private string DefaultWeightPath => Path.Combine(DefaultFilePath, "weight.json");

        // Validate and normalize path (same safety behavior as other settings pages)
        private string NormalizeAndValidatePath(string? incomingPath)
        {
            string resultPath;
            if (string.IsNullOrWhiteSpace(incomingPath))
            {
                resultPath = DefaultWeightPath;
            }
            else
            {
                if (!Path.IsPathRooted(incomingPath))
                    resultPath = Path.Combine(DefaultFilePath, incomingPath);
                else
                    resultPath = incomingPath;
            }

            resultPath = Path.GetFullPath(resultPath);
            var allowedPrefix = DefaultFilePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
            if (!resultPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(resultPath, DefaultFilePath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The provided file path is outside of the allowed settings folder.");

            return resultPath;
        }

        private void PopulatePortAndOptions()
        {
            try
            {
                AvailablePorts = SerialPort.GetPortNames().OrderBy(n => n).ToList();
            }
            catch
            {
                AvailablePorts = new List<string>();
            }

            if (!AvailablePorts.Contains(Model.PortName) && !string.IsNullOrWhiteSpace(Model.PortName))
                AvailablePorts.Insert(0, Model.PortName);

            if (!CommonBaudRates.Contains(Model.BaudRate))
                CommonBaudRates.Insert(0, Model.BaudRate);

            if (!DataBitsOptions.Contains(Model.DataBits))
                DataBitsOptions.Insert(0, Model.DataBits);

            if (!ParityOptions.Contains(Model.Parity) && !string.IsNullOrWhiteSpace(Model.Parity))
                ParityOptions.Insert(0, Model.Parity);

            if (!StopBitsOptions.Contains(Model.StopBits) && !string.IsNullOrWhiteSpace(Model.StopBits))
                StopBitsOptions.Insert(0, Model.StopBits);
        }

        public async Task<IActionResult> OnGetAsync(string? filePath)
        {
            try
            {
                var path = NormalizeAndValidatePath(filePath);
                var loaded = await _fileSettings.ReadAsync<WeightSettings>(path);
                if (loaded != null)
                    Model = loaded;

                PopulatePortAndOptions();
                ViewData["SettingsFilePath"] = path;
                return Page();
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "Invalid settings file path.");
                ViewData["SettingsFilePath"] = DefaultWeightPath;
                PopulatePortAndOptions();
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Could not read weight settings: " + ex.Message);
                ViewData["SettingsFilePath"] = DefaultWeightPath;
                PopulatePortAndOptions();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync(string? filePath)
        {
            if (!ModelState.IsValid)
            {
                PopulatePortAndOptions();
                ViewData["SettingsFilePath"] = filePath ?? DefaultWeightPath;
                return Page();
            }

            try
            {
                var path = NormalizeAndValidatePath(filePath);
                await _fileSettings.WriteAsync(path, Model);
                TempData["Message"] = "Saved successfully to " + path;

                // Use absolute page path to ensure RedirectToPage resolves
                return RedirectToPage("/Settings/EditWeight", new { filePath = path });
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "Invalid settings file path.");
                PopulatePortAndOptions();
                ViewData["SettingsFilePath"] = DefaultWeightPath;
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Could not save weight settings: " + ex.Message);
                PopulatePortAndOptions();
                ViewData["SettingsFilePath"] = filePath ?? DefaultWeightPath;
                return Page();
            }
        }
    }
}
