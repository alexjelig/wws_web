using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using wws_web.Models;
using wws_web.Services;

namespace wws_web.Pages
{
    public class ReadSerialModel : PageModel
    {
        private readonly IFileSettingsService _fileSettings;
        private readonly IConfiguration _config;

        public ReadSerialModel(IFileSettingsService fileSettings, IConfiguration config)
        {
            _fileSettings = fileSettings;
            _config = config;
        }

        // Bound properties used for display / post-back (if you want to allow override via form)
        [BindProperty]
        public string PortName { get; set; } = string.Empty;

        public string? Weight { get; private set; }
        public string? Error { get; private set; }

        // Default path logic matching other settings pages
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

        // Read scanner settings from file (returns defaults when file missing or on error)
        private async Task<ScannerSettings> LoadScannerSettingsAsync(string? filePath = null)
        {
            try
            {
                string path;
                if (string.IsNullOrWhiteSpace(filePath))
                    path = DefaultScannerPath;
                else
                    path = Path.IsPathRooted(filePath) ? Path.GetFullPath(filePath) : Path.GetFullPath(Path.Combine(DefaultFilePath, filePath));

                var settings = await _fileSettings.ReadAsync<ScannerSettings>(path);
                return settings ?? new ScannerSettings();
            }
            catch
            {
                // On any error, return defaults (caller will display message if needed)
                return new ScannerSettings();
            }
        }

        public void OnGet()
        {
            // Optional: prefill PortName with saved value so UI shows it (if you render a form)
            // We'll load synchronously from file for quick prefill (avoid throwing)
            try
            {
                var settingsTask = LoadScannerSettingsAsync();
                settingsTask.Wait();
                var s = settingsTask.Result;
                PortName = s.PortName ?? string.Empty;
            }
            catch
            {
                PortName = string.Empty;
            }
        }

        public async Task OnPostAsync()
        {
            Error = null;
            Weight = null;

            // Load saved scanner settings
            var settings = await LoadScannerSettingsAsync();

            // If user provided an override in the form, prefer it
            var portToOpen = !string.IsNullOrWhiteSpace(PortName) ? PortName : settings.PortName;
            var baud = settings.BaudRate <= 0 ? 9600 : settings.BaudRate;
            var dataBits = settings.DataBits <= 0 ? 8 : settings.DataBits;

            // Map parity/stopbits strings to System.IO.Ports enums
            System.IO.Ports.Parity parity = System.IO.Ports.Parity.None;
            if (!string.IsNullOrWhiteSpace(settings.Parity) &&
                Enum.TryParse<System.IO.Ports.Parity>(settings.Parity, true, out var p))
            {
                parity = p;
            }

            System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One;
            if (!string.IsNullOrWhiteSpace(settings.StopBits) &&
                Enum.TryParse<System.IO.Ports.StopBits>(settings.StopBits, true, out var sb))
            {
                stopBits = sb;
            }

            try
            {
                // Use whichever SerialPort wrapper you already have.
                // Here we presume SerialPortService can accept these options.
                var serial = new SerialPortService(portToOpen, baud, parity, dataBits, stopBits);
                Weight = serial.ReadLine();
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
        }
    }
}
