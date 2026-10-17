using System;
using System.Collections.Generic;
using System.IO;

namespace PopupCleaner.Models
{
    public sealed class CleanScanResult
    {
        public long TotalBytes { get; set; }
        public int FileCount { get; set; }
        public int Skipped { get; set; }
        public List<string> Targets { get; private set; }

        public CleanScanResult()
        {
            Targets = new List<string>();
        }

        public string SizeText { get { return MemoryStatus.Format((ulong)Math.Max(0L, TotalBytes)); } }
    }

    public sealed class CleanResult
    {
        public long FreedBytes { get; set; }
        public int Deleted { get; set; }
        public int Failed { get; set; }

        public string FreedText { get { return MemoryStatus.Format((ulong)Math.Max(0L, FreedBytes)); } }
    }

    /// <summary>临时文件清理：扫描 / 清理 %TEMP%、Windows\Temp、INetCache 等目录。</summary>
    public static class TempCleaner
    {
        private sealed class Counter
        {
            public int Value;
        }

        public static IList<string> DefaultTargets()
        {
            var list = new List<string>();
            AddUnique(list, Path.GetTempPath());
            AddUnique(list, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
            AddUnique(list, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"));
            AddUnique(list, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\Windows\INetCache"));
            return list;
        }

        private static void AddUnique(List<string> list, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string trimmed = path.TrimEnd('\\');
            foreach (string existing in list)
            {
                if (string.Equals(existing.TrimEnd('\\'), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            list.Add(path);
        }

        public static CleanScanResult Scan(IList<string> dirs)
        {
            var result = new CleanScanResult();
            foreach (string dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                result.Targets.Add(dir);
                var skipped = new Counter();
                foreach (string file in EnumerateFilesSafe(dir, skipped))
                {
                    try
                    {
                        result.TotalBytes += new FileInfo(file).Length;
                        result.FileCount++;
                    }
                    catch
                    {
                        result.Skipped++;
                    }
                }
                result.Skipped += skipped.Value;
            }
            return result;
        }

        public static CleanResult Clean(IList<string> dirs, Action<string> log)
        {
            var result = new CleanResult();
            foreach (string dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    if (log != null)
                    {
                        log("跳过（不存在）：" + dir);
                    }
                    continue;
                }

                var skipped = new Counter();
                foreach (string file in EnumerateFilesSafe(dir, skipped))
                {
                    try
                    {
                        long len = new FileInfo(file).Length;
                        try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                        File.Delete(file);
                        result.FreedBytes += len;
                        result.Deleted++;
                    }
                    catch
                    {
                        result.Failed++;
                    }
                }

                RemoveEmptyDirs(dir);
                if (log != null)
                {
                    log("清理目录 " + dir + "：本目录处理完成");
                }
            }
            return result;
        }

        private static IEnumerable<string> EnumerateFilesSafe(string root, Counter skipped)
        {
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                string dir = stack.Pop();

                string[] files;
                try
                {
                    files = Directory.GetFiles(dir);
                }
                catch
                {
                    skipped.Value++;
                    files = null;
                }

                if (files != null)
                {
                    foreach (string f in files)
                    {
                        yield return f;
                    }
                }

                string[] subs;
                try
                {
                    subs = Directory.GetDirectories(dir);
                }
                catch
                {
                    subs = null;
                }

                if (subs == null)
                {
                    continue;
                }

                foreach (string sub in subs)
                {
                    try
                    {
                        FileAttributes attr = File.GetAttributes(sub);
                        if ((attr & FileAttributes.ReparsePoint) != 0)
                        {
                            skipped.Value++;
                            continue;
                        }
                        stack.Push(sub);
                    }
                    catch
                    {
                        skipped.Value++;
                    }
                }
            }
        }

        private static void RemoveEmptyDirs(string root)
        {
            var dirs = new List<string>();
            try
            {
                dirs.AddRange(Directory.GetDirectories(root, "*", SearchOption.AllDirectories));
            }
            catch
            {
                try { dirs.AddRange(Directory.GetDirectories(root)); } catch { }
            }

            dirs.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
            foreach (string d in dirs)
            {
                try
                {
                    if (Directory.GetFileSystemEntries(d).Length == 0)
                    {
                        Directory.Delete(d);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
