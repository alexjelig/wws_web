using System;

namespace wws_web.Models
{
    public class AppSetup
    {
        public string ApplicationName { get; set; } = "wws_web";
        public string Environment { get; set; } = "Development";
        public int Port { get; set; } = 5000;
        public bool EnableDiagnostics { get; set; } = false;
        // Add any other application-level settings you need
        public double ZeroBand { get; set; } = 0.0;
    }
}
