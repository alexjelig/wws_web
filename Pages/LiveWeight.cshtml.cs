using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using wws_web.Models;
using wws_web.Services;
using System.Diagnostics;

namespace wws_web.Pages
{
    public class LiveWeightModel : PageModel
    {
        private readonly IFileSettingsService _fileSettings;
        private readonly IConfiguration _config;
        private readonly DeviceManager _devices;

        public LiveWeightModel(IFileSettingsService fileSettings, IConfiguration config, DeviceManager devices)
        {
            _fileSettings = fileSettings;
            _config = config;
            _devices = devices;
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

        // OnGetAsync: prefill UI and ACQUIRE the weight device so the port is opened while user is on this page.
        // The matching Release will be attempted from client-side on unload (sendBeacon), and there's a server-side Release handler as well.
        public async Task OnGetAsync()
        {
            // Prefill optional override from saved settings for convenience in UI
            var settings = await LoadWeightSettingsAsync();
            PortNameOverride = settings.PortName ?? string.Empty;

            // Attempt to Acquire the Baykon weight reader so the port is opened while the user is on this page.
            try
            {
                var weightReader = _devices.Get<BaykonWeightReaderService>("BaykonWeightReader");
                if (weightReader != null)
                {
                    // If PortNameOverride provided, let caller change the reader's port before Acquire if desired.
                    if (!string.IsNullOrWhiteSpace(PortNameOverride))
                        weightReader.SerialPortName = PortNameOverride;

                    weightReader.Acquire();
                    Debug.WriteLine("[LiveWeight] Acquired BaykonWeightReader on OnGetAsync. Ref=" + weightReader.CurrentRefCount);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LiveWeight] Acquire failed: " + ex.Message);
                // Don't throw — keep page usable, but show an error when trying to read
            }
        }

        // Keep existing ad-hoc read method (left in place per your request to keep unrelated functions)
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

        // Handler that the page's JS will poll for a live weight. Called with ?handler=GetWeight
        public JsonResult OnGetGetWeight()
        {
            try
            {
                var settingsTask = LoadWeightSettingsAsync();
                settingsTask.Wait();
                var settings = settingsTask.Result;

                var weightReader = _devices.Get<BaykonWeightReaderService>("BaykonWeightReader");
                if (weightReader == null)
                {
                    return new JsonResult(new { success = false, error = "Weight reader service not available" });
                }

                BaykonWeightResult result;

                // Prefer serial if a port is configured; otherwise attempt TCP read
                var effectivePort = !string.IsNullOrWhiteSpace(PortNameOverride) ? PortNameOverride : settings.PortName;
                if (!string.IsNullOrWhiteSpace(effectivePort))
                {
                    // Ensure the port is open. Acquire may have been called in OnGetAsync; if not, attempt to Acquire here.
                    var acquiredHere = false;
                    try
                    {
                        if (!weightReader.IsOpen)
                        {
                            weightReader.Acquire();
                            acquiredHere = true;
                        }

                        result = weightReader.ReadWeightFromSerial();
                    }
                    finally
                    {
                        // If we acquired just for this call, release immediately (but normally the page holds an Acquire)
                        if (acquiredHere)
                        {
                            weightReader.Release();
                        }
                    }
                }
                else
                {
                    // TCP fallback (does not require Acquire)
                    result = weightReader.ReadWeightFromTcp();
                }

                if (result.Success)
                {
                    return new JsonResult(new { success = true, weight = result.GrossWeight });
                }

                return new JsonResult(new { success = false, error = result.ErrorMessage });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LiveWeight] OnGetGetWeight error: " + ex.Message);
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        // Release handler called from client using sendBeacon on unload (best-effort).
        // We disable antiforgery because sendBeacon won't include it.
        [IgnoreAntiforgeryToken]
        public JsonResult OnPostRelease()
        {
            try
            {
                var weightReader = _devices.Get<BaykonWeightReaderService>("BaykonWeightReader");
                if (weightReader != null)
                {
                    weightReader.Release();
                    Debug.WriteLine("[LiveWeight] OnPostRelease called; ref=" + weightReader.CurrentRefCount);
                    return new JsonResult(new { ok = true });
                }

                return new JsonResult(new { ok = false, error = "Weight reader not found" });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LiveWeight] OnPostRelease error: " + ex.Message);
                return new JsonResult(new { ok = false, error = ex.Message });
            }
        }
    }
}
