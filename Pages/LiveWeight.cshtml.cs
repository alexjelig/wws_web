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
    public class LiveWeightModel : PageModel
    {
        private readonly IFileSettingsService _fileSettings;
        private readonly IConfiguration _config;

        public LiveWeightModel(IFileSettingsService fileSettings, IConfiguration config)
        {
            _fileSettings = fileSettings;
            _config = config;
        }

        // optional override from UI (if you add an input)
        [BindProperty]
        public string PortNameOverride { get; set; } = string.Empty;

        public string? Weight { get; private set; }
        public string? Error { get; private set; }

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

        private async Task<WeightSettings> LoadWeightSettingsAsync(string? filePath = null)
        {
            try
            {
                var path = string.IsNullOrWhiteSpace(filePath)
                    ? DefaultWeightPath
                    : (Path.IsPathRooted(filePath) ? Path.GetFullPath(filePath) : Path.GetFullPath(Path.Combine(DefaultFilePath, filePath)));

                var settings = await _fileSettings.ReadAsync<WeightSettings>(path);
                return settings ?? new WeightSettings();
            }
            catch
            {
                return new WeightSettings();
            }
        }

        public async Task OnGetAsync()
        {
            // Prefill optional override from saved settings for convenience in UI
            var settings = await LoadWeightSettingsAsync();
            PortNameOverride = settings.PortName ?? string.Empty;
        }

        public async Task OnPostReadAsync()
        {
            Weight = null;
            Error = null;

            var settings = await LoadWeightSettingsAsync();

            var port = !string.IsNullOrWhiteSpace(PortNameOverride) ? PortNameOverride : settings.PortName;
            var baud = settings.BaudRate <= 0 ? 9600 : settings.BaudRate;
            var dataBits = settings.DataBits <= 0 ? 8 : settings.DataBits;

            // map parity/stopbits strings to enums
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
                // use a reasonable read timeout (ms)
                var serial = new SerialPortService(port, baud, parity, dataBits, stopBits, readTimeout: 3000);
                var result = serial.ReadLine();
                // SerialPortService returns "Error: ..." on failure by design in your current implementation
                if (result != null && result.StartsWith("Error:"))
                {
                    Error = result;
                }
                else
                {
                    Weight = result;
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message + " (Line 112)";
            }
        }
    }
}
