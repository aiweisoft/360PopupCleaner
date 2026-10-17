using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PopupCleaner.Models
{
    public sealed class MemoryStatus
    {
        public ulong TotalBytes { get; set; }
        public ulong AvailBytes { get; set; }
        public int UsedPercent { get; set; }

        public string TotalText { get { return Format(TotalBytes); } }
        public string AvailText { get { return Format(AvailBytes); } }

        public static string Format(ulong bytes)
        {
            double mb = bytes / 1024.0 / 1024.0;
            if (mb >= 1024.0)
            {
                return (mb / 1024.0).ToString("0.00") + " GB";
            }
            return mb.ToString("0") + " MB";
        }
    }

    /// <summary>
    /// 内存整理：清空各进程工作集（EmptyWorkingSet）+ 清空系统待机内存列表（Standby List）。
    /// 二者都需要管理员权限；待机列表清理还需 SeProfileSingleProcessPrivilege。
    /// </summary>
    public static class MemoryOptimizer
    {
        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(
            int SystemInformationClass, ref int SystemInformation, int SystemInformationLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_SET_QUOTA = 0x0100;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x0002;

        private const int SystemMemoryListInformation = 0x50; // 80
        private const int MemoryPurgeStandbyList = 4;

        public static MemoryStatus ReadStatus()
        {
            var status = new MemoryStatus();
            try
            {
                MEMORYSTATUSEX m = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(m))
                {
                    status.TotalBytes = m.ullTotalPhys;
                    status.AvailBytes = m.ullAvailPhys;
                    status.UsedPercent = (int)m.dwMemoryLoad;
                }
            }
            catch
            {
            }
            return status;
        }

        /// <summary>对每个可访问进程调用 EmptyWorkingSet。返回成功数量，skipped 为无法处理的进程数。</summary>
        public static int TrimWorkingSets(out int skipped)
        {
            int done = 0;
            skipped = 0;

            Process[] all;
            try
            {
                all = Process.GetProcesses();
            }
            catch
            {
                return 0;
            }

            foreach (Process p in all)
            {
                IntPtr h = IntPtr.Zero;
                try
                {
                    h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, false, p.Id);
                    if (h == IntPtr.Zero)
                    {
                        skipped++;
                        continue;
                    }

                    if (EmptyWorkingSet(h))
                    {
                        done++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch
                {
                    skipped++;
                }
                finally
                {
                    if (h != IntPtr.Zero)
                    {
                        try { CloseHandle(h); } catch { }
                    }
                }
            }

            return done;
        }

        /// <summary>清空系统待机内存列表。需要相应权限；失败返回 false。</summary>
        public static bool PurgeStandbyList()
        {
            if (!EnablePrivilege("SeProfileSingleProcessPrivilege"))
            {
                return false;
            }

            try
            {
                int cmd = MemoryPurgeStandbyList;
                return NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int)) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool EnablePrivilege(string name)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(new IntPtr(-1), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                {
                    return false;
                }

                LUID luid;
                if (!LookupPrivilegeValue(null, name, out luid))
                {
                    return false;
                }

                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.PrivilegeCount = 1;
                tp.Luid = luid;
                tp.Attributes = SE_PRIVILEGE_ENABLED;

                if (!AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    return false;
                }

                return Marshal.GetLastWin32Error() == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (token != IntPtr.Zero)
                {
                    try { CloseHandle(token); } catch { }
                }
            }
        }
    }
}
