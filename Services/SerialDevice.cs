using System;
using System.IO.Ports;
using System.Diagnostics;

namespace wws_web.Services
{
    public abstract class SerialDevice : IDisposable
    {
        protected SerialPort? _port;
        protected readonly object _lock = new();

        public string Name { get; }
        public bool IsOpen => _port?.IsOpen ?? false;

        protected SerialDevice(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        // Generic open method (used by derived class after reading config)
        protected void Open(string portName, int baud, int dataBits, Parity parity, StopBits stopBits)
        {
            Close();

            _port = new SerialPort
            {
                PortName = portName,
                BaudRate = baud,
                DataBits = dataBits,
                Parity = parity,
                StopBits = stopBits,
                NewLine = "\r\n",
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            _port.DataReceived += OnDataReceived;

            Debug.WriteLine($"[SerialDevice:{Name}] Attempting to open {portName} @ {baud}");
            _port.Open();
            Debug.WriteLine($"[SerialDevice:{Name}] Port opened: {portName}");
        }

        protected abstract void OnDataReceived(object sender, SerialDataReceivedEventArgs e);

        public virtual void Close()
        {
            try
            {
                if (_port != null)
                {
                    _port.DataReceived -= OnDataReceived;

                    if (_port.IsOpen)
                    {
                        _port.Close();
                        Debug.WriteLine($"[SerialDevice:{Name}] Port closed.");
                    }

                    _port.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SerialDevice:{Name}] Close error: {ex.Message}");
            }

            _port = null;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
