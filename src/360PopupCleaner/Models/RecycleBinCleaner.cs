using System;
using System.Runtime.InteropServices;

namespace PopupCleaner.Models
{
    /// <summary>回收站：查询大小与清空。</summary>
    public static class RecycleBinCleaner
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO
        {
            public uint cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        /// <summary>返回回收站占用字节数，items 为项目数；失败返回 0。</summary>
        public static long QuerySize(out long items)
        {
            items = 0;
            try
            {
                SHQUERYRBINFO info = new SHQUERYRBINFO();
                info.cbSize = (uint)Marshal.SizeOf(typeof(SHQUERYRBINFO));
                int hr = SHQueryRecycleBin(null, ref info);
                if (hr == 0)
                {
                    items = info.i64NumItems;
                    return info.i64Size;
                }
            }
            catch
            {
            }
            return 0;
        }

        public static bool Empty(out string error)
        {
            error = null;
            try
            {
                int hr = SHEmptyRecycleBin(
                    IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                if (hr == 0)
                {
                    return true;
                }

                error = "HRESULT 0x" + hr.ToString("X8");
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
