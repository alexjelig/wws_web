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
    public class EditScannerModel : PageModel
    {
        private readonly IFileSettingsService _fileSettings;
        private readonly IConfiguration _config;
        private readonly DeviceManager _deviceManager;

        public EditScannerModel(IFileSettingsService fileSettings, IConfiguration config, DeviceManager deviceManager)
        {
            _fileSettings = fileSettings;
            _config = config;
            _deviceManager = deviceManager; // Inject DeviceManager
        }

        [BindProperty]
        public ScannerSettings Model { get; set; } = new ScannerSettings();

        // UI helpers
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

        private string DefaultScannerPath => Path.Combine(DefaultFilePath, "scanner.json");

        // Validate and normalize path (same safety behavior as AppSetup)
        private string NormalizeAndValidatePath(string? incomingPath)
        {
            string resultPath;
            if (string.IsNullOrWhiteSpace(incomingPath))
            {
                resultPath = DefaultScannerPath;
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
                // If enumeration fails, fallback to empty list
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
                var loaded = await _fileSettings.ReadAsync<ScannerSettings>(path);
                if (loaded != null)
                    Model = loaded;

                PopulatePortAndOptions();
                ViewData["SettingsFilePath"] = path;
                return Page();
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "Invalid settings file path.");
                ViewData["SettingsFilePath"] = DefaultScannerPath;
                PopulatePortAndOptions();
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Could not read scanner settings: " + ex.Message);
                ViewData["SettingsFilePath"] = DefaultScannerPath;
                PopulatePortAndOptions();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync(string? filePath)
        {
            if (!ModelState.IsValid)
            {
                PopulatePortAndOptions();
                ViewData["SettingsFilePath"] = filePath ?? DefaultScannerPath;
                return Page();
            }

            try
            {
                var path = NormalizeAndValidatePath(filePath);
                await _fileSettings.WriteAsync(path, Model);

                // Reconfigure the ScannerDevice dynamically
                _deviceManager.ReconfigureSerialDevice("Scanner", Model);

                TempData["Message"] = "Saved successfully to " + path;

                return RedirectToPage("/Settings/EditScanner", new { filePath = path });
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "Invalid settings file path.");
                PopulatePortAndOptions();
                ViewData["SettingsFilePath"] = DefaultScannerPath;
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Could not save scanner settings: " + ex.Message);
                PopulatePortAndOptions();
                ViewData["SettingsFilePath"] = filePath ?? DefaultScannerPath;
                return Page();
            }
        }
    }
}
