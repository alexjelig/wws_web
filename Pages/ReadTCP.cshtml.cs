using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Sockets;
using System.Text;

namespace wws_web.Pages
{
    public class ReadTCPModel : PageModel
    {
        [BindProperty]
        public string ServerIP { get; set; } = "127.0.0.1"; // Default for testing
        [BindProperty]
        public int ServerPort { get; set; } = 12345;        // Default port for your device

        public string? Weight { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            // Optionally set defaults
        }

        public void OnPost()
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(ServerIP, ServerPort);
                    using (NetworkStream stream = client.GetStream())
                    {
                        // If you need to send a command/request, do it here:
                        // byte[] request = Encoding.ASCII.GetBytes("YOUR_COMMAND");
                        // stream.Write(request, 0, request.Length);

                        // Read response (assuming ASCII and that weight ends with newline)
                        byte[] buffer = new byte[256];
                        int bytes = stream.Read(buffer, 0, buffer.Length);
                        string response = Encoding.ASCII.GetString(buffer, 0, bytes);
                        Weight = response.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
