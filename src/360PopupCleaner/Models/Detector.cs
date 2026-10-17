using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace PopupCleaner.Models
{
    public sealed class DetectResult
    {
        public bool Installed { get; set; }
        public string InstallDir { get; set; }
        public string Version { get; set; }
        public IList<string> RunningProcesses { get; set; }
        public bool StatKeyPresent { get; set; }
    }

    /// <summary>检测本机是否安装并运行 360 安全卫士。</summary>
    public static class Detector
    {
        private const string StatKey = @"SOFTWARE\WOW6432Node\360Safe\stat";

        private static readonly string[] WatchProcesses =
        {
            "360Safe", "360tray", "360SettingCenter", "SoftMgrLite",
            "360leakfixer", "ZhuDongFangYu", "360sd", "360rp",
        };

        private static readonly string DefaultInstallDir = @"C:\Program Files (x86)\360\360Safe";

        public static DetectResult Detect()
        {
            var result = new DetectResult
            {
                RunningProcesses = new List<string>(),
                InstallDir = DefaultInstallDir,
            };

            result.StatKeyPresent = ProbeKey(StatKey);

            // 运行中的 360 进程
            foreach (string name in WatchProcesses)
            {
                try
                {
                    Process[] found = Process.GetProcessesByName(name);
                    if (found.Length > 0)
                    {
                        result.RunningProcesses.Add(name);
                    }
                }
                catch
                {
                    // 忽略单个进程查询失败。
                }
            }

            // 版本：读主程序文件版本
            string mainExe = Path.Combine(DefaultInstallDir, "360Safe.exe");
            if (File.Exists(mainExe))
            {
                try
                {
                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(mainExe);
                    result.Version = info.ProductVersion ?? info.FileVersion;
                }
                catch
                {
                    result.Version = null;
                }

                result.Installed = true;
            }

            if (!result.Installed && result.StatKeyPresent)
            {
                // 注册表在但没找到默认路径，也算已安装。
                result.Installed = true;
            }

            return result;
        }

        private static bool ProbeKey(string subKey)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, false))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
