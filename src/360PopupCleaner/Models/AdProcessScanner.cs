using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PopupCleaner.Models
{
    public sealed class AdProcessItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }

        public string Display
        {
            get { return (Name ?? "?") + "  (PID " + Id + ")"; }
        }
    }

    /// <summary>
    /// 检测并结束 360 的广告 / 资讯弹窗进程。
    /// 这些模块常驻在 %APPDATA%\360safe\SoftMgr 下（360 核心程序在 Program Files，不在此目录），
    /// 典型：sesvcr.exe（mininews 资讯弹窗）、MultiTip.exe（今日精选）、webapp.exe（网页广告容器）。
    /// 注册表开关只能阻止“以后”不再拉起弹窗，已经在显示的弹窗必须结束进程才会消失。
    /// </summary>
    public static class AdProcessScanner
    {
        private static readonly string[] KnownNames =
        {
            "sesvcr", "MultiTip", "webapp", "360DayPop", "360newsld", "360SpeedldHealth",
        };

        /// <summary>广告模块目录：%APPDATA%\360safe\SoftMgr。</summary>
        public static string AdModuleDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "360safe", "SoftMgr");
            }
        }

        public static bool IsAdProcess(string name, string path)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (string known in KnownNames)
                {
                    if (string.Equals(name, known, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(path))
            {
                string dir = AdModuleDir;
                if (path.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static IList<AdProcessItem> Find()
        {
            var list = new List<AdProcessItem>();

            Process[] all;
            try
            {
                all = Process.GetProcesses();
            }
            catch
            {
                return list;
            }

            foreach (Process p in all)
            {
                string name;
                string path;
                try
                {
                    name = p.ProcessName;
                }
                catch
                {
                    continue;
                }

                try
                {
                    path = p.MainModule != null ? p.MainModule.FileName : null;
                }
                catch
                {
                    path = null;
                }

                if (IsAdProcess(name, path))
                {
                    list.Add(new AdProcessItem { Id = p.Id, Name = name, Path = path });
                }
            }

            list.Sort(delegate(AdProcessItem a, AdProcessItem b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        public static bool Kill(int pid, out string error)
        {
            error = null;
            try
            {
                Process p = Process.GetProcessById(pid);
                p.Kill();
                p.WaitForExit(3000);
                return true;
            }
            catch (ArgumentException)
            {
                // 进程已经不在了，视作成功。
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
