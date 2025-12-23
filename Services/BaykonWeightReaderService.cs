using System;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Globalization;
using System.Diagnostics;

namespace wws_web.Services
{
    public class BaykonWeightResult
    {
        public bool Success { get; set; }
        public string RawData { get; set; } = string.Empty;
        public float GrossWeight { get; set; }
        public float TareWeight { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // BaykonWeightReaderService now manages a single SerialPort instance and provides
    // Acquire()/Release() semantics so the port is opened only when needed.
    public class BaykonWeightReaderService : IDisposable
    {
        // Configurable defaults (public so you may set via UI/settings before Acquire)
        public string SerialPortName { get; set; } = "COM6";
        public int BaudRate { get; set; } = 9600;
        public int SerialTimeout { get; set; } = 1000; // ms

        public string TcpHost { get; set; } = "127.0.0.1";
        public int TcpPort { get; set; } = 502;
        public int TcpTimeout { get; set; } = 1000; // ms

        // Internal state
        private SerialPort? _port;
        private readonly object _lock = new();
        private int _refCount = 0;

        // Exposed diagnostics
        public int CurrentRefCount
        {
            get
            {
                lock (_lock) { return _refCount; }
            }
        }

        public bool IsOpen
        {
            get
            {
                lock (_lock) { return _port?.IsOpen ?? false; }
            }
        }

        // Acquire: increments refcount and opens port if first acquirer
        public void Acquire()
        {
            lock (_lock)
            {
                if (_refCount == 0)
                {
                    try
                    {
                        OpenPort();
                        Debug.WriteLine($"[BaykonWeightReader] Port {_port?.PortName} opened.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[BaykonWeightReader] OpenPort failed: {ex.Message}");
                        // Do not swallow — but we keep _refCount 0 so callers can handle exception
                        throw;
                    }
                }

                _refCount++;
                Debug.WriteLine($"[BaykonWeightReader] Acquire -> refCount={_refCount}");
            }
        }

        // Release: decrements refcount and closes port when it reaches 0
        public void Release()
        {
            lock (_lock)
            {
                if (_refCount <= 0)
                {
                    Debug.WriteLine("[BaykonWeightReader] Release called but refCount already 0.");
                    _refCount = 0;
                    return;
                }

                _refCount--;
                Debug.WriteLine($"[BaykonWeightReader] Release -> refCount={_refCount}");

                if (_refCount == 0)
                {
                    try
                    {
                        ClosePort();
                        Debug.WriteLine("[BaykonWeightReader] Port closed due to refCount==0.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[BaykonWeightReader] ClosePort error: {ex.Message}");
                    }
                }
            }
        }

        // Open the SerialPort instance (internal, called under lock)
        private void OpenPort()
        {
            if (_port != null && _port.IsOpen)
                return;

            _port = new SerialPort(SerialPortName, BaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = SerialTimeout,
                WriteTimeout = 1000,
                NewLine = "\r\n"
            };

            _port.Open();
        }

        // Close and dispose of the SerialPort (internal, called under lock)
        private void ClosePort()
        {
            if (_port == null)
                return;

            try
            {
                if (_port.IsOpen)
                {
                    // attempt a graceful close
                    _port.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BaykonWeightReader] Exception while closing port: {ex.Message}");
            }
            finally
            {
                try
                {
                    _port.Dispose();
                }
                catch { }
                _port = null;
            }
        }

        // Read weight from the serial port that is expected to be already opened via Acquire().
        // If the port isn't open, this method returns an error result (caller should Acquire first).
        public BaykonWeightResult ReadWeightFromSerial()
        {
            var result = new BaykonWeightResult();
            byte[] dataBytes = new byte[32];
            int bytes2Read = 15;

            lock (_lock)
            {
                if (_port == null || !_port.IsOpen)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Port {SerialPortName} is not open. Call Acquire() before attempting to read.";
                    Debug.WriteLine($"[BaykonWeightReader] ReadWeightFromSerial failed: {result.ErrorMessage}");
                    return result;
                }

                try
                {
                    // Wait for STX (0x02)
                    var sw = Stopwatch.StartNew();
                    while (true)
                    {
                        if (sw.ElapsedMilliseconds > SerialTimeout)
                            throw new TimeoutException($"Timeout waiting for STX from serial device on port {SerialPortName}.");

                        int readChar = _port.ReadChar();
                        if (readChar == 0x02)
                            break;
                    }

                    // Wait until the expected bytes are available
                    sw.Restart();
                    while (_port.BytesToRead < bytes2Read)
                    {
                        if (sw.ElapsedMilliseconds > SerialTimeout)
                            throw new TimeoutException($"Timeout waiting for data bytes from serial device on port {SerialPortName}.");
                        // small spin; could Thread.Sleep(1) but we keep it tight for responsiveness
                    }

                    int read = _port.Read(dataBytes, 0, bytes2Read);
                    if (read <= 0)
                        throw new TimeoutException("No bytes read from serial device.");

                    string data = Encoding.ASCII.GetString(dataBytes, 0, read);
                    result.RawData = data;
                    ParseBaykonData(data, read, result);
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Port {SerialPortName}: {ex.Message}";
                    Debug.WriteLine($"[BaykonWeightReader] ReadWeightFromSerial error: {ex.Message}");
                }
            }

            return result;
        }

        // Keep the existing TCP read implementation (unchanged)
        public BaykonWeightResult ReadWeightFromTcp()
        {
            var result = new BaykonWeightResult();
            byte[] dataBytes = new byte[32];
            int bytes2Read = 15;
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(TcpHost, TcpPort);
                if (!connectTask.Wait(TcpTimeout))
                    throw new TimeoutException("Timeout connecting to TCP device.");

                var ns = tcpClient.GetStream();
                ns.ReadTimeout = TcpTimeout;

                // Wait for STX (0x02)
                var sw = Stopwatch.StartNew();
                while (true)
                {
                    if (sw.ElapsedMilliseconds > TcpTimeout)
                        throw new TimeoutException("Timeout waiting for STX from TCP stream.");

                    int b = ns.ReadByte();
                    if (b == -1)
                        throw new TimeoutException("TCP stream closed while waiting for STX.");
                    if (b == 0x02)
                        break;
                }

                // Wait for enough bytes
                sw.Restart();
                while (tcpClient.Available < bytes2Read)
                {
                    if (sw.ElapsedMilliseconds > TcpTimeout)
                        throw new TimeoutException("Timeout waiting for data from TCP device.");
                }

                int bytesRead = ns.Read(dataBytes, 0, bytes2Read);
                if (bytesRead <= 0)
                    throw new TimeoutException("TCP stream closed while reading data.");

                string data = Encoding.ASCII.GetString(dataBytes, 0, bytesRead);

                result.RawData = data;
                ParseBaykonData(data, bytesRead, result);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Success = false;
            }
            return result;
        }

        private void ParseBaykonData(string data, int bytes2Read, BaykonWeightResult result)
        {
            if (data.Length != bytes2Read)
            {
                result.ErrorMessage = "Invalid data length.";
                result.Success = false;
                return;
            }

            string grossStr = data.Substring(3, 6);
            string tareStr = data.Substring(9, 6);

            if (string.IsNullOrWhiteSpace(tareStr) || tareStr.Trim() == "0")
                tareStr = "-1";

            if (!float.TryParse(grossStr.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float gross))
                gross = 0;
            if (!float.TryParse(tareStr.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float tare))
                tare = 0;

            result.GrossWeight = gross;
            result.TareWeight = tare;
            result.Success = true;
        }

        // Dispose pattern to ensure port is closed if the service is disposed
        public void Dispose()
        {
            try
            {
                lock (_lock)
                {
                    _refCount = 0;
                    ClosePort();
                }
            }
            catch { }
        }
    }
}
