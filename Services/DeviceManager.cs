using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using wws_web.Models;

namespace wws_web.Services
{
    public class DeviceManager
    {
        public DeviceManager()
        {
            Debug.WriteLine("[DeviceManager] Initialized.");
        }
        private readonly Dictionary<string, object> _devices = new();

        public void Register(string name, object device)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            _devices[name] = device;
            Debug.WriteLine($"[DeviceManager] Registered device '{name}' type={device.GetType().Name}");

            // IMPORTANT: Do not start/open serial ports here.
            // Device startup (Start/Acquire) must be done explicitly by the consumer
            // (page, hub or service) to avoid claiming hardware at application startup.
        }

        public T? Get<T>(String name) where T : class
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (_devices.TryGetValue(name, out var obj) && obj is T typed)
            {
                return typed;
            }

            return null;
        }
        /*
        public bool OpenSerialDevice(string name, string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits)
        {
            var device = Get<SerialDevice>(name);
            if (device != null)
            {
                device.Open(portName, baudRate, dataBits, parity, stopBits);
                return true;
            }
            return false;
        }

        public bool OpenTCPDevice<T>(string name, Func<T, bool> openFunc) where T : class
        {
            var device = Get<T>(name);
            if (device != null)
            {
                return openFunc(device);
            }
            return false;
        }

        public void Close(string name)
        {
            var device = Get<IDisposable>(name);
            device?.Dispose();
        }

        public void CloseAll()
        {
            foreach (var device in _devices.Values)
            {
                var disposable = device as IDisposable;
                disposable?.Dispose();
            }
        }
	
	    public void ReconfigureSerialDevice(string name, ScannerSettings settings)
        {
            var device = Get<SerialDevice>(name);
            if (device != null)
            {
                device.Close(); // Close the device first
                device.Open(settings.PortName, settings.BaudRate, settings.DataBits,
                    Enum.Parse<Parity>(settings.Parity), Enum.Parse<StopBits>(settings.StopBits));
            }
        }
        */

        public void ReconfigureSerialDevice(string name, ScannerSettings settings)
        {
            var scanner = Get<ScannerDevice>(name);
            if (scanner == null)
            {
                Debug.WriteLine($"[DeviceManager] ReconfigureSerialDevice: scanner '{name}' not found.");
                return;
            }

            scanner.ApplySettings(settings, persistToFile: true);
            Debug.WriteLine($"[DeviceManager] ReconfigureSerialDevice: scanner '{name}' reconfigured.");
        }

    }
}

