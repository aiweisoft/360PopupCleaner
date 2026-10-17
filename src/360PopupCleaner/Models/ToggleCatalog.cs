using System.Collections.Generic;
using Microsoft.Win32;

namespace PopupCleaner.Models
{
    /// <summary>
    /// 已知的 360 广告 / 资讯 / 推送开关清单。
    /// 全部取自本机 360 安全卫士 14.0.1.1310 的实测键值；
    /// 每项都把 HKLM\SOFTWARE\WOW6432Node\360Safe 下的具体值名写清楚，UI 中透明展示。
    /// </summary>
    public static class ToggleCatalog
    {
        private const string StatPath = @"SOFTWARE\WOW6432Node\360Safe\stat";
        private const string ExamUiPath = @"SOFTWARE\WOW6432Node\360Safe\examinui";
        private const string SoftMgrPath = @"SOFTWARE\WOW6432Node\360Safe\softmgr";

        private const string GPopup = "广告与资讯弹窗";
        private const string GTips = "推广 / 提示";
        private const string GHome = "首页与软件管家";
        private const string GExp = "实验性（参考第三方工具）";

        public static IList<ToggleDefinition> CreateAll()
        {
            return new List<ToggleDefinition>
            {
                Def("ad", GPopup, "广告弹窗", "360 广告投放总开关（adinfoflag）。设为 0 即关闭。", "本机实测",
                    StatPath, "adinfoflag", 0, 1, "adinfoflag_closetm"),

                Def("news", GPopup, "资讯 / 热点弹窗", "360 资讯与热点推送开关（newsinfoflag）。设为 0 即关闭。", "本机实测",
                    StatPath, "newsinfoflag", 0, 1, "newsinfoflag_closetm"),

                Def("speedld_ad", GPopup, "加速球广告位", "桌面加速球内嵌的广告位（speedld_ad）。", "本机实测",
                    StatPath, "speedld_ad", 0, 1, null),

                Def("home_news", GPopup, "安全首页资讯", "安全卫士首页的资讯 / 看点模块（HomeNews）。", "本机实测",
                    ExamUiPath, "HomeNews", 0, 1, null),

                Def("speedld_health", GTips, "加速球健康提醒", "加速球弹出的健康 / 体检提醒（show_speedldhealth）。", "本机实测",
                    StatPath, "show_speedldhealth", 0, 1, null),

                Def("perf_remind", GTips, "性能 / 清理提醒", "性能占用与“该清理了”类提醒弹窗（perf_remind_show）。", "本机实测",
                    StatPath, "perf_remind_show", 0, 1, null),

                Def("videodl", GTips, "视频下载提示", "检测到视频时弹出的下载提示（videodltip）。", "本机实测",
                    StatPath, "videodltip", 0, 1, null),

                Def("kantu", GTips, "看图推广提示", "看图相关推广提示（kantu）。", "本机实测",
                    StatPath, "kantu", 0, 1, null),

                Def("apktip", GTips, "安卓应用提示", "安卓 / APK 相关提示（apktip）。", "本机实测",
                    StatPath, "apktip", 0, 1, null),

                Def("sm_new", GHome, "软件管家新版推荐", "软件管家“新软推荐 / 新版本”推广（softmgr_shownew）。", "本机实测",
                    SoftMgrPath, "softmgr_shownew", 0, 1, null),

                Def("sm_weather", GHome, "软件管家天气推送", "软件管家面板的天气推送（ptweather）。", "本机实测",
                    SoftMgrPath, "ptweather", 0, 1, null),

                Def("noadpop", GExp, "弹窗过滤伪装（noadpop）", "写入 noadpop=1，让 360 认为已开启弹窗过滤模式。参考 AzureIvory/block-ads，效果未逐一验证。", "block-ads 参考",
                    StatPath, "noadpop", 1, 0, null),

                Def("advtool_popwndtracker", GExp, "弹窗追踪器开关（advtool_PopWndTracker）", "写入 advtool_PopWndTracker=1，配合上面的伪装。参考 AzureIvory/block-ads，效果未逐一验证。", "block-ads 参考",
                    StatPath, "advtool_PopWndTracker", 1, 0, null),
            };
        }

        private static ToggleDefinition Def(
            string id, string group, string title, string description, string source,
            string subKey, string valueName, int disableValue, int enableValue, string closeTimeName)
        {
            return new ToggleDefinition
            {
                Id = id,
                Group = group,
                Title = title,
                Description = description,
                Source = source,
                Hive = RegistryHive.LocalMachine,
                View = RegistryView.Registry64,
                SubKey = subKey,
                ValueName = valueName,
                DisableValue = disableValue,
                EnableValue = enableValue,
                CloseTimeValueName = closeTimeName,
            };
        }
    }
}
