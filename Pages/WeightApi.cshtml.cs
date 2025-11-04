using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using wws_web.Models;
using wws_web.Services;

namespace wws_web.Pages
{
    public class WeightApiModel : PageModel
    {
        private readonly IFileSettingsService _fileSettings;
        private readonly IConfiguration _config;
        private readonly ILogger<WeightApiModel> _logger;

        public WeightApiModel(IFileSettingsService fileSettings, IConfiguration config, ILogger<WeightApiModel> logger)
        {
            _fileSettings = fileSettings;
            _config = config;
            _logger = logger;
        }

        private string DefaultFilePath
        {
            get
            {
                var configured = _config.GetValue<string>("SettingsBasePath")?.Trim();
                var basePath = string.IsNullOrWhiteSpace(configured) ? @"C:\wws" : configured;
                var full = Path.GetFullPath(basePath);
                if (!Directory.Exists(full)) Directory.CreateDirectory(full);
                return full;
            }
        }

        private string DefaultWeightPath => Path.Combine(DefaultFilePath, "weight.json");

        private async Task<WeightSettings> LoadWeightSettingsAsync()
        {
            try
            {
                var settings = await _fileSettings.ReadAsync<WeightSettings>(DefaultWeightPath);
                if (settings == null)
                {
                    _logger.LogWarning("Weight settings file not found; using defaults.");
                    return new WeightSettings();
                }
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load weight settings; using defaults.");
                return new WeightSettings();
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var settings = await LoadWeightSettingsAsync();

            var reader = new BaykonWeightReaderService
            {
                SerialPortName = settings.PortName ?? "COM6", // Fallback to COM6 if settings missing
                BaudRate = settings.BaudRate <= 0 ? 9600 : settings.BaudRate,
                SerialTimeout = 1000 // Default timeout; update if weight.json adds timeout
            };

            var result = reader.ReadWeightFromSerial();
            return new JsonResult(new
            {
                weight = result.GrossWeight,
                success = result.Success,
                error = result.ErrorMessage
            });
        }
    }
}
