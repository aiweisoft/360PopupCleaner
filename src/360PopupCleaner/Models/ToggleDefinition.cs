using Microsoft.Win32;

namespace PopupCleaner.Models
{
    /// <summary>
    /// 一个可写入 / 可还原的 360 注册表开关定义。
    /// 目标位于 HKLM\SOFTWARE\WOW6432Node\360Safe 下（显式使用 64 位视图 + WOW6432Node 路径，
    /// 与进程位宽无关，读取的就是 360 实际写入的那个键）。
    /// </summary>
    public sealed class ToggleDefinition
    {
        public string Id { get; set; }
        public string Group { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        /// <summary>依据来源（如“本机实测”“block-ads 参考”）。</summary>
        public string Source { get; set; }

        public RegistryHive Hive { get; set; }
        public RegistryView View { get; set; }

        /// <summary>相对 HIVE 的子键路径，例如 SOFTWARE\WOW6432Node\360Safe\stat。</summary>
        public string SubKey { get; set; }

        public string ValueName { get; set; }

        /// <summary>写入该值即代表“关闭此广告 / 推送”。</summary>
        public int DisableValue { get; set; }

        /// <summary>写入该值即代表“开启此广告 / 推送”。</summary>
        public int EnableValue { get; set; }

        /// <summary>可选：关闭时同时写入时间戳的值名（模仿 360 自身记录关闭时间的行为）。</summary>
        public string CloseTimeValueName { get; set; }

        public string FullValuePath
        {
            get
            {
                string hive = Hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
                return hive + "\\" + SubKey + "\\" + ValueName;
            }
        }
    }
}
