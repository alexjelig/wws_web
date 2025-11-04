using System;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Globalization;

namespace wws_web.Services
{
    public class BaykonWeightResult
    {
        public bool Success { get; set; }
        public string RawData { get; set; } = string.Empty;
        public float GrossWeight { get; set; }
        public float TareWeight { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        // Add more as needed (Status, flags, etc.)
    }

    public class BaykonWeightReaderService
    {
        public string SerialPortName { get; set; } = "COM6";
        public int BaudRate { get; set; } = 9600;
        public int SerialTimeout { get; set; } = 1000; // ms

        public string TcpHost { get; set; } = "127.0.0.1";
        public int TcpPort { get; set; } = 502;
        public int TcpTimeout { get; set; } = 1000; // ms
                                                   // Initialize to empty strings to satisfy nullable analysis and avoid null checks later
        public string RawData { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public BaykonWeightResult ReadWeightFromSerial()
        {
            var result = new BaykonWeightResult();
            byte[] dataBytes = new byte[32];
            int bytes2Read = 15;
            try
            {
                using var comport = new SerialPort(SerialPortName, BaudRate, Parity.None, 8, StopBits.One);
                comport.ReadTimeout = SerialTimeout;
                comport.Open();

                // Wait for STX (0x02)
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    if (sw.ElapsedMilliseconds > SerialTimeout)
                        throw new TimeoutException($"Timeout waiting for STX from serial device on port {SerialPortName}.");

                    int readChar = comport.ReadChar();
                    if (readChar == 0x02)
                        break;
                }

                // Wait for enough bytes
                sw = System.Diagnostics.Stopwatch.StartNew();
                while (comport.BytesToRead < bytes2Read)
                {
                    if (sw.ElapsedMilliseconds > SerialTimeout)
                        throw new TimeoutException($"Timeout waiting for data bytes from serial device on port {SerialPortName}.");
                }

                comport.Read(dataBytes, 0, bytes2Read);
                string data = Encoding.ASCII.GetString(dataBytes, 0, bytes2Read);

                result.RawData = data;
                ParseBaykonData(data, bytes2Read, result);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Port {SerialPortName}: {ex.Message}";
                result.Success = false;
            }
            return result;
        }

        public void SetRawData(string data)
        {
            RawData = data ?? string.Empty;
        }

        public void SetError(string message)
        {
            ErrorMessage = message ?? string.Empty;
        }

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
                var sw = System.Diagnostics.Stopwatch.StartNew();
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
                sw = System.Diagnostics.Stopwatch.StartNew();
                while (tcpClient.Available < bytes2Read)
                {
                    if (sw.ElapsedMilliseconds > TcpTimeout)
                        throw new TimeoutException("Timeout waiting for data from TCP device.");
                }

                int bytesRead = ns.Read(dataBytes, 0, bytes2Read);
                if (bytesRead <= 0)
                    throw new TimeoutException("TCP stream closed while reading data.");

                string data = Encoding.ASCII.GetString(dataBytes, 0, bytes2Read);

                result.RawData = data;
                ParseBaykonData(data, bytes2Read, result);
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
        }
    }
}
