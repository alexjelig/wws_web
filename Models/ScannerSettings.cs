using System;

namespace wws_web.Models
{
    public class ScannerSettings
    {
        // Serial port name, e.g. "COM3"
        public string PortName { get; set; } = "COM1";

        // Standard baud rates
        public int BaudRate { get; set; } = 9600;

        // Data bits (5,6,7,8)
        public int DataBits { get; set; } = 8;

        // Parity as string ("None","Odd","Even","Mark","Space")
        public string Parity { get; set; } = "None";

        // StopBits as string ("None","One","Two","OnePointFive")
        public string StopBits { get; set; } = "One";

        // Any other scanner-specific settings can go here
    }
}
