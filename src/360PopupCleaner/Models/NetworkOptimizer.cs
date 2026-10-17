using System;
using System.Diagnostics;
using System.Text;

namespace PopupCleaner.Models
{
    /// <summary>网络优化：刷新 DNS 缓存、重置 Winsock、重置 TCP/IP。</summary>
    public static class NetworkOptimizer
    {
        public static int FlushDns(out string output)
        {
            return Run("ipconfig.exe", "/flushdns", out output);
        }

        public static int ResetWinsock(out string output)
        {
            return Run("netsh.exe", "winsock reset", out output);
        }

        public static int ResetTcpIp(out string output)
        {
            return Run("netsh.exe", "int ip reset", out output);
        }

        public static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            string t = text.Trim();
            int idx = t.IndexOfAny(new[] { '\r', '\n' });
            return idx >= 0 ? t.Substring(0, idx).Trim() : t;
        }

        private static int Run(string file, string args, out string output)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Default,
                    StandardErrorEncoding = Encoding.Default,
                };

                using (Process p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(60000);
                    output = (stdout + " " + stderr).Trim();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return -1;
            }
        }
    }
}
