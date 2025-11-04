using System;
using System.IO.Ports;

namespace wws_web.Services
{
    /// <summary>
    /// Lightweight wrapper around System.IO.Ports.SerialPort.
    /// Now accepts full serial configuration: port, baud, parity, data bits and stop bits.
    /// ReadLine returns the line read or throws / returns error text on failure.
    /// </summary>
    public class SerialPortService
    {
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly Parity _parity;
        private readonly int _dataBits;
        private readonly StopBits _stopBits;
        private readonly int _readTimeout;

        // New constructor accepts all common serial parameters.
        // readTimeout is milliseconds for ReadLine; use -1 for infinite (not recommended).
        public SerialPortService(
            string portName,
            int baudRate = 9600,
            Parity parity = Parity.None,
            int dataBits = 8,
            StopBits stopBits = StopBits.One,
            int readTimeout = 3000)
        {
            _portName = portName ?? throw new ArgumentNullException(nameof(portName));
            _baudRate = baudRate;
            _parity = parity;
            _dataBits = dataBits;
            _stopBits = stopBits;
            _readTimeout = readTimeout;
        }

        // Reads a single line from the serial port. Returns the data or an error message.
        public string ReadLine()
        {
            using var serialPort = new SerialPort(_portName)
            {
                BaudRate = _baudRate,
                Parity = _parity,
                DataBits = _dataBits,
                StopBits = _stopBits,
                NewLine = "\n",
                ReadTimeout = _readTimeout,
                WriteTimeout = 1000
            };

            try
            {
                serialPort.Open();

                // Attempt to read a line
                var line = serialPort.ReadLine();
                return line ?? string.Empty;
            }
            catch (TimeoutException)
            {
                // Timeout-specific error message with port name
                return $"Error: Timed out on port {_portName}.";
            }
            catch (UnauthorizedAccessException uaex)
            {
                return $"Error: Port {_portName} is in use or access denied: {uaex.Message}";
            }
            catch (Exception ex)
            {
                // Generic error message
                return $"Error: An issue occurred on port {_portName}: {ex.Message}";
            }
            finally
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
        }
    }
}
