using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace PopupCleaner.Models
{
    public sealed class StartupItem
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string LocationLabel { get; set; }
        public bool IsEnabled { get; set; }

        /// <summary>StartupApproved 子键路径（相对 hive）。</summary>
        public string ApprovedKey { get; set; }

        /// <summary>StartupApproved 中的值名（Run 项为值名，启动文件夹为文件名）。</summary>
        public string ApprovedValueName { get; set; }

        public RegistryHive Hive { get; set; }
    }

    /// <summary>
    /// 开机启动项管理。禁用 / 启用使用 Windows 自身的 StartupApproved 机制：
    /// 在 *\Explorer\StartupApproved\Run(或 Run32 / StartupFolder)\&lt;名称&gt; 写入 12 字节二进制
    /// （首字节 0x02 = 启用，0x03 = 禁用），Run 值本身保留 —— 与任务管理器一致，非破坏性、可逆。
    /// </summary>
    public static class StartupManager
    {
        private const string RunCurrentUser = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunLocalMachine = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunLocalMachine32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";

        private const string ApprovedRunCurrentUser =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedRunLocalMachine =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedRunLocalMachine32 =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        private const string ApprovedFolderCurrentUser =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
        private const string ApprovedFolderLocalMachine =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        public static IList<StartupItem> Enumerate()
        {
            var list = new List<StartupItem>();

            AddRun(list, RegistryHive.CurrentUser, RunCurrentUser, ApprovedRunCurrentUser, "当前用户");
            AddRun(list, RegistryHive.LocalMachine, RunLocalMachine, ApprovedRunLocalMachine, "所有用户");
            AddRun(list, RegistryHive.LocalMachine, RunLocalMachine32, ApprovedRunLocalMachine32, "所有用户(32位)");

            AddFolder(list, RegistryHive.CurrentUser,
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                ApprovedFolderCurrentUser, "当前用户·启动文件夹");
            AddFolder(list, RegistryHive.LocalMachine,
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                ApprovedFolderLocalMachine, "所有用户·启动文件夹");

            list.Sort(delegate(StartupItem a, StartupItem b)
            {
                int byLoc = string.Compare(a.LocationLabel, b.LocationLabel, StringComparison.OrdinalIgnoreCase);
                return byLoc != 0 ? byLoc : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        public static bool SetEnabled(StartupItem item, bool enabled, out string error)
        {
            error = null;
            try
            {
                byte[] data = new byte[12];
                data[0] = enabled ? (byte)0x02 : (byte)0x03;

                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(item.Hive, RegistryView.Registry64))
                using (RegistryKey key = baseKey.CreateSubKey(item.ApprovedKey))
                {
                    if (key == null)
                    {
                        error = "无法创建 StartupApproved 键：" + item.ApprovedKey;
                        return false;
                    }

                    key.SetValue(item.ApprovedValueName, data, RegistryValueKind.Binary);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void AddRun(List<StartupItem> list, RegistryHive hive, string runSubKey,
            string approvedSubKey, string label)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (RegistryKey run = baseKey.OpenSubKey(runSubKey))
                {
                    if (run == null)
                    {
                        return;
                    }

                    foreach (string name in run.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name))
                        {
                            continue;
                        }

                        string command = run.GetValue(name) as string;
                        list.Add(new StartupItem
                        {
                            Name = name,
                            Command = command ?? "",
                            LocationLabel = label + "·注册表",
                            Hive = hive,
                            ApprovedKey = approvedSubKey,
                            ApprovedValueName = name,
                            IsEnabled = ReadEnabled(hive, approvedSubKey, name),
                        });
                    }
                }
            }
            catch
            {
            }
        }

        private static void AddFolder(List<StartupItem> list, RegistryHive hive, string folder,
            string approvedSubKey, string label)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch
            {
                return;
            }

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                if (string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(new StartupItem
                {
                    Name = fileName,
                    Command = file,
                    LocationLabel = label,
                    Hive = hive,
                    ApprovedKey = approvedSubKey,
                    ApprovedValueName = fileName,
                    IsEnabled = ReadEnabled(hive, approvedSubKey, fileName),
                });
            }
        }

        private static bool ReadEnabled(RegistryHive hive, string approvedSubKey, string valueName)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(approvedSubKey))
                {
                    if (key == null)
                    {
                        return true;
                    }

                    byte[] data = key.GetValue(valueName) as byte[];
                    if (data == null || data.Length == 0)
                    {
                        return true;
                    }

                    byte state = data[0];
                    return state != 0x03 && state != 0x06;
                }
            }
            catch
            {
                return true;
            }
        }
    }
}
