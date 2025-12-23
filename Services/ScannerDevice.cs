using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using wws_web.Models;

namespace wws_web.Services
{
    // Represents the scanner hardware; reads from a serial port defined in scanner.json
    // Provides LatestTag retrieval and acknowledgment semantics.
    public class ScannerDevice : SerialDevice
    {
        private readonly string _configPath;
        private string _buffer = "";
        private const int MaxTagLength = 128;
        private bool _started = false;
        private int _refCount = 0;

        public void Acquire()
        {
            lock (_lock)
            {
                if (_refCount == 0)
                {
                    Debug.WriteLine("[ScannerDevice] Acquire: refcount 0 -> starting device.");
                    Start(); // Start opens port (LoadConfig + Open)
                }
                _refCount++;
                Debug.WriteLine("[ScannerDevice] Acquire: refcount -> {0}", _refCount);
            }
        }

        public void Release()
        {
            lock (_lock)
            {
                if (_refCount <= 0)
                {
                    Debug.WriteLine("[ScannerDevice] Release called but refcount already 0.");
                    _refCount = 0;
                    return;
                }

                _refCount--;
                Debug.WriteLine("[ScannerDevice] Release: refcount -> {0}", _refCount);

                if (_refCount == 0)
                {
                    Debug.WriteLine("[ScannerDevice] Release: refcount 0 -> closing device.");
                    Close();
                    _started = false;
                }
            }
        }

        public int CurrentRefCount
        {
            get
            {
                lock (_lock)
                {
                    return _refCount;
                }
            }
        }

        // Tag state
        private string _latestTag = "";
        private bool _tagConsumed = false;

        public ScannerDevice(string name, string? configPath = null) : base(name)
        {
            _configPath = configPath ?? @"C:\wws\scanner.json";
            Debug.WriteLine($"[ScannerDevice] Created with name: {name}, configPath: {_configPath}");
        }

        public void Start()
        {
            if (_started)
            {
                Debug.WriteLine("[ScannerDevice] Start() ignored (already started).");
                return;
            }

            Debug.WriteLine("[ScannerDevice] Start() - loading configuration file.");

            SerialConfig? cfg = LoadConfig();
            if (cfg == null)
            {
                Debug.WriteLine("[ScannerDevice] Configuration load failed. Device not started.");
                return;
            }

            try
            {
                var parity = ParseEnum<Parity>(cfg.Parity, Parity.None);
                var stopBits = ParseEnum<StopBits>(cfg.StopBits, StopBits.One);

                Open(cfg.PortName, cfg.BaudRate, cfg.DataBits, parity, stopBits);
                _started = true;
                Debug.WriteLine("[ScannerDevice] Start complete, port open and listening.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerDevice] Failed to open port: {ex.Message}");
            }
        }

        public void Restart()
        {
            Debug.WriteLine("[ScannerDevice] Restart() called.");
            Close();
            _started = false;
            Start();
        }

        private SerialConfig? LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    Debug.WriteLine($"[ScannerDevice] Config file not found at {_configPath}");
                    return null;
                }

                string json = File.ReadAllText(_configPath);
                var cfg = JsonSerializer.Deserialize<SerialConfig>(json);

                if (cfg == null)
                {
                    Debug.WriteLine("[ScannerDevice] Config deserialization returned null.");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(cfg.PortName))
                {
                    Debug.WriteLine("[ScannerDevice] Config invalid: PortName missing.");
                    return null;
                }

                Debug.WriteLine($"[ScannerDevice] Config loaded: Port={cfg.PortName}, Baud={cfg.BaudRate}, DataBits={cfg.DataBits}, Parity={cfg.Parity}, StopBits={cfg.StopBits}");
                return cfg;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerDevice] LoadConfig error: {ex.Message}");
                return null;
            }
        }

        protected override void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var sp = _port;
                if (sp == null || !sp.IsOpen)
                {
                    return;
                }

                string raw = sp.ReadExisting();
                if (string.IsNullOrEmpty(raw))
                {
                    Debug.WriteLine("[ScannerDevice] DataReceived empty raw string.");
                    return;
                }

                Debug.WriteLine($"[ScannerDevice] Raw bytes: {EscapeSpecials(raw)}");

                lock (_lock)
                {
                    _buffer += raw;

                    // If we got terminators (CR/LF/STX/ETX) process buffered data
                    if (_buffer.Contains("\r") || _buffer.Contains("\n") || _buffer.Contains("\u0002") || _buffer.Contains("\u0003"))
                    {
                        string cleaned = _buffer
                            .Replace("\u0002", "")   // STX
                            .Replace("\u0003", "")   // ETX
                            .Replace("\r", "")
                            .Replace("\n", "")
                            .Trim();

                        Debug.WriteLine($"[ScannerDevice] Cleaned candidate: '{cleaned}'");

                        if (!string.IsNullOrEmpty(cleaned))
                        {
                            if (cleaned.Length <= MaxTagLength)
                            {
                                _latestTag = cleaned;
                                _tagConsumed = false;
                                Debug.WriteLine($"[ScannerDevice] LatestTag updated to: '{_latestTag}'");
                            }
                            else
                            {
                                Debug.WriteLine($"[ScannerDevice] Ignored tag length {cleaned.Length} > {MaxTagLength}");
                            }
                        }

                        _buffer = "";
                        Debug.WriteLine("[ScannerDevice] Buffer cleared after processing.");
                    }
                    else
                    {
                        Debug.WriteLine($"[ScannerDevice] Buffer length now {_buffer.Length}, waiting for terminator.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerDevice] OnDataReceived error: {ex.Message}");
            }
        }

        public string ConsumeLatest()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_latestTag) && !_tagConsumed)
                {
                    _tagConsumed = true;
                    Debug.WriteLine($"[ScannerDevice] ConsumeLatest returning '{_latestTag}'");
                    return _latestTag;
                }

                return "";
            }
        }

        public void AcknowledgeTag()
        {
            lock (_lock)
            {
                Debug.WriteLine($"[ScannerDevice] AcknowledgeTag clearing '{_latestTag}'");
                _tagConsumed = false;
                _latestTag = "";
            }
        }

        public override void Close()
        {
            Debug.WriteLine("[ScannerDevice] Close() called.");
            base.Close();
        }

        private static T ParseEnum<T>(string? value, T fallback) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }
            if (Enum.TryParse<T>(value, true, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static string EscapeSpecials(string s)
        {
            return s
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\u0002", "\\x02")
                .Replace("\u0003", "\\x03");
        }

        public void ApplySettings(ScannerSettings settings, bool persistToFile = true)
        {
            if (settings == null)
            {
                Debug.WriteLine("[ScannerDevice] ApplySettings called with null settings.");
                return;
            }

            Debug.WriteLine($"[ScannerDevice] ApplySettings: Port={settings.PortName}, Baud={settings.BaudRate}, DataBits={settings.DataBits}, Parity={settings.Parity}, StopBits={settings.StopBits}, persistToFile={persistToFile}");

            try
            {
                var parity = ParseEnum<Parity>(settings.Parity, Parity.None);
                var stopBits = ParseEnum<StopBits>(settings.StopBits, StopBits.One);

                Close();
                _started = false;

                Open(settings.PortName, settings.BaudRate, settings.DataBits, parity, stopBits);
                _started = true;
                Debug.WriteLine("[ScannerDevice] ApplySettings -> port re-opened.");

                if (persistToFile)
                {
                    SaveSettingsToConfig(settings);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerDevice] ApplySettings error: {ex.Message}");
            }
        }

        private void SaveSettingsToConfig(ScannerSettings settings)
        {
            try
            {
                var cfg = new SerialConfig
                {
                    PortName = settings.PortName,
                    BaudRate = settings.BaudRate,
                    DataBits = settings.DataBits,
                    Parity = settings.Parity,
                    StopBits = settings.StopBits
                };

                var json = System.Text.Json.JsonSerializer.Serialize(cfg, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_configPath, json);
                Debug.WriteLine($"[ScannerDevice] Saved new settings to {_configPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerDevice] SaveSettingsToConfig error: {ex.Message}");
            }
        }

    }

    // Configuration model for scanner.json
    public class SerialConfig
    {
        public string? PortName { get; set; }
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public string? Parity { get; set; }
        public string? StopBits { get; set; }
    }
}
