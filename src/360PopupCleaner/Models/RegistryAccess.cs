using System;
using System.Globalization;
using Microsoft.Win32;

namespace PopupCleaner.Models
{
    /// <summary>单个注册表值的快照，用于备份 / 还原。</summary>
    public sealed class RegValueSnapshot
    {
        public string SubKey { get; set; }
        public string Name { get; set; }
        public bool Exists { get; set; }

        /// <summary>DWord / Sz / None。</summary>
        public string Kind { get; set; }

        public long Number { get; set; }
        public string Text { get; set; }
    }

    /// <summary>注册表读写与快照工具，统一走 64 位视图。</summary>
    public static class RegistryAccess
    {
        private const RegistryHive Machine = RegistryHive.LocalMachine;
        private const RegistryView View64 = RegistryView.Registry64;

        public static int UnixNow()
        {
            return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static bool TryReadInt(ToggleDefinition def, out int value)
        {
            value = 0;
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(def.Hive, def.View))
                using (RegistryKey key = baseKey.OpenSubKey(def.SubKey, false))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    object raw = key.GetValue(def.ValueName, null);
                    if (raw == null)
                    {
                        return false;
                    }

                    value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool WriteDword(ToggleDefinition def, int value, out string error)
        {
            return WriteDwordValue(def.SubKey, def.ValueName, value, out error);
        }

        public static bool WriteDwordValue(string subKey, string name, int value, out string error)
        {
            error = null;
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(Machine, View64))
                using (RegistryKey key = baseKey.CreateSubKey(subKey, true))
                {
                    if (key == null)
                    {
                        error = "无法打开或创建注册表项：" + subKey;
                        return false;
                    }

                    key.SetValue(name, value, RegistryValueKind.DWord);
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                error = "访问被拒绝：需要管理员权限，或键被安全软件保护。";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void DeleteValue(string subKey, string name)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(Machine, View64))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(name, false);
                    }
                }
            }
            catch
            {
                // 删除失败不致命：还原时若原值不存在，删不掉通常是因为本来就没有。
            }
        }

        public static RegValueSnapshot Capture(string subKey, string name)
        {
            var snapshot = new RegValueSnapshot
            {
                SubKey = subKey,
                Name = name,
                Exists = false,
                Kind = "None",
                Number = 0,
                Text = null,
            };

            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(Machine, View64))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, false))
                {
                    if (key == null)
                    {
                        return snapshot;
                    }

                    object raw = key.GetValue(name, null);
                    if (raw == null)
                    {
                        return snapshot;
                    }

                    snapshot.Exists = true;
                    RegistryValueKind kind = key.GetValueKind(name);
                    snapshot.Kind = kind.ToString();

                    if (kind == RegistryValueKind.DWord || kind == RegistryValueKind.QWord)
                    {
                        snapshot.Number = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        snapshot.Text = Convert.ToString(raw, CultureInfo.InvariantCulture);
                    }
                }
            }
            catch
            {
                // 读不到就当不存在。
            }

            return snapshot;
        }

        public static bool Apply(RegValueSnapshot snapshot, out string error)
        {
            error = null;

            if (!snapshot.Exists)
            {
                DeleteValue(snapshot.SubKey, snapshot.Name);
                return true;
            }

            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(Machine, View64))
                using (RegistryKey key = baseKey.CreateSubKey(snapshot.SubKey, true))
                {
                    if (key == null)
                    {
                        error = "无法打开注册表项：" + snapshot.SubKey;
                        return false;
                    }

                    if (string.Equals(snapshot.Kind, "DWord", StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(snapshot.Name, (int)snapshot.Number, RegistryValueKind.DWord);
                    }
                    else if (string.Equals(snapshot.Kind, "QWord", StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(snapshot.Name, snapshot.Number, RegistryValueKind.QWord);
                    }
                    else if (string.Equals(snapshot.Kind, "Sz", StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(snapshot.Name, snapshot.Text ?? string.Empty, RegistryValueKind.String);
                    }
                    else if (string.Equals(snapshot.Kind, "ExpandString", StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(snapshot.Name, snapshot.Text ?? string.Empty, RegistryValueKind.ExpandString);
                    }
                    else
                    {
                        key.SetValue(snapshot.Name, snapshot.Text ?? string.Empty, RegistryValueKind.String);
                    }
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                error = "还原被拒绝：需要管理员权限。";
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
