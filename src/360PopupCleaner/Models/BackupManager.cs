using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace PopupCleaner.Models
{
    /// <summary>
    /// 修改前的注册表原值备份，写到 %LOCALAPPDATA%\360PopupCleaner\backups。
    /// 每次“应用更改”生成一个带时间戳的 JSON；还原时读取最近一次。
    /// </summary>
    public static class BackupManager
    {
        public static string RootDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "360PopupCleaner");
            }
        }

        public static string BackupDir
        {
            get { return Path.Combine(RootDir, "backups"); }
        }

        public static string Save(IList<RegValueSnapshot> snapshots, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(BackupDir);

                string fileName = DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
                string path = Path.Combine(BackupDir, fileName);

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(new List<RegValueSnapshot>(snapshots));
                File.WriteAllText(path, json, new UTF8Encoding(false));

                return path;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        public static string LatestBackupPath()
        {
            if (!Directory.Exists(BackupDir))
            {
                return null;
            }

            string[] files = Directory.GetFiles(BackupDir, "*.json");
            if (files.Length == 0)
            {
                return null;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files[files.Length - 1];
        }

        public static IList<RegValueSnapshot> Load(string path, out string error)
        {
            error = null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var serializer = new JavaScriptSerializer();
                var list = serializer.Deserialize<List<RegValueSnapshot>>(json);
                return list ?? new List<RegValueSnapshot>();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }
    }
}
