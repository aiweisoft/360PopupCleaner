using System;
using System.Collections.ObjectModel;
using System.Windows;
using PopupCleaner.Models;

namespace PopupCleaner.ViewModels
{
    public sealed class SystemOptimizeViewModel : ObservableObject
    {
        private MemoryStatus _memory;
        private string _memorySummary;
        private string _memoryDetail;
        private string _tempSummary;
        private string _recycleSummary;
        private string _statusMessage;

        public SystemOptimizeViewModel()
        {
            Log = new ObservableCollection<string>();
            StartupItems = new ObservableCollection<StartupItemViewModel>();

            RefreshCommand = new RelayCommand(Refresh);
            OptimizeAllCommand = new RelayCommand(OptimizeAll);
            MemoryCommand = new RelayCommand(OptimizeMemory);
            TempCommand = new RelayCommand(CleanTemp);
            RecycleCommand = new RelayCommand(EmptyRecycle);
            NetworkCommand = new RelayCommand(OptimizeNetwork);

            Refresh();
        }

        public ObservableCollection<string> Log { get; private set; }
        public ObservableCollection<StartupItemViewModel> StartupItems { get; private set; }

        public RelayCommand RefreshCommand { get; private set; }
        public RelayCommand OptimizeAllCommand { get; private set; }
        public RelayCommand MemoryCommand { get; private set; }
        public RelayCommand TempCommand { get; private set; }
        public RelayCommand RecycleCommand { get; private set; }
        public RelayCommand NetworkCommand { get; private set; }

        public string MemorySummary
        {
            get { return _memorySummary; }
            private set { SetProperty(ref _memorySummary, value); }
        }

        public string MemoryDetail
        {
            get { return _memoryDetail; }
            private set { SetProperty(ref _memoryDetail, value); }
        }

        public string TempSummary
        {
            get { return _tempSummary; }
            private set { SetProperty(ref _tempSummary, value); }
        }

        public string RecycleSummary
        {
            get { return _recycleSummary; }
            private set { SetProperty(ref _recycleSummary, value); }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        private void AppendLog(string message)
        {
            Log.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            while (Log.Count > 400)
            {
                Log.RemoveAt(0);
            }
        }

        private void Refresh()
        {
            ScanMemory();
            ScanTemp();
            ScanRecycle();
            LoadStartup();

            StatusMessage = "就绪。";
            AppendLog("已刷新系统状态。");
        }

        private void ScanMemory()
        {
            _memory = MemoryOptimizer.ReadStatus();
            MemorySummary = "内存 " + _memory.TotalText + " · 已用 " + _memory.UsedPercent + "% · 可用 " + _memory.AvailText;

            if (string.IsNullOrEmpty(MemoryDetail))
            {
                MemoryDetail = "整理前可用：" + _memory.AvailText;
            }
        }

        private void ScanTemp()
        {
            CleanScanResult scan = TempCleaner.Scan(TempCleaner.DefaultTargets());
            TempSummary = "临时文件可释放约 " + scan.SizeText + "（" + scan.FileCount + " 个文件）";
        }

        private void ScanRecycle()
        {
            long items;
            long size = RecycleBinCleaner.QuerySize(out items);
            RecycleSummary = "回收站占用 " + MemoryStatus.Format((ulong)Math.Max(0L, size)) + "（" + items + " 项）";
        }

        private void LoadStartup()
        {
            StartupItems.Clear();
            foreach (StartupItem item in StartupManager.Enumerate())
            {
                StartupItems.Add(new StartupItemViewModel(item, AppendLog));
            }
            AppendLog("已载入 " + StartupItems.Count + " 个开机启动项。");
        }

        private void OptimizeMemory()
        {
            MemoryStatus before = MemoryOptimizer.ReadStatus();

            int skipped;
            int done = MemoryOptimizer.TrimWorkingSets(out skipped);
            bool purged = MemoryOptimizer.PurgeStandbyList();

            MemoryStatus after = MemoryOptimizer.ReadStatus();
            _memory = after;

            MemorySummary = "内存 " + after.TotalText + " · 已用 " + after.UsedPercent + "% · 可用 " + after.AvailText;
            MemoryDetail = "整理前可用 " + before.AvailText + " → 整理后 " + after.AvailText;

            long delta = (long)after.AvailBytes - (long)before.AvailBytes;
            string deltaText = (delta >= 0 ? "+" : "-") + MemoryStatus.Format((ulong)Math.Abs(delta));

            StatusMessage = "内存整理完成：已整理 " + done + " 个进程工作集"
                + (purged ? "，并清空待机内存列表" : "，待机内存列表未能清空（权限不足）")
                + "，可用内存变化 " + deltaText + "。";

            AppendLog("内存整理：工作集 " + done + " 个（跳过 " + skipped + "），待机列表 "
                + (purged ? "已清空" : "未清空") + "，可用变化 " + deltaText + "。");
        }

        private void CleanTemp()
        {
            CleanScanResult scan = TempCleaner.Scan(TempCleaner.DefaultTargets());
            if (scan.TotalBytes <= 0)
            {
                StatusMessage = "没有可清理的临时文件。";
                AppendLog("临时文件扫描：无可清理内容。");
                ScanTemp();
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                "即将清理临时文件，预计释放约 " + scan.SizeText + "（" + scan.FileCount + " 个文件）。\n"
                + "正在使用中的文件会自动跳过。是否继续？",
                "清理临时文件", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK)
            {
                AppendLog("用户取消了临时文件清理。");
                return;
            }

            CleanResult result = TempCleaner.Clean(TempCleaner.DefaultTargets(), AppendLog);
            ScanTemp();

            StatusMessage = "临时文件清理完成：释放 " + result.FreedText + "，删除 " + result.Deleted
                + " 个文件，失败 " + result.Failed + " 个。";
            AppendLog("临时文件清理完成：释放 " + result.FreedText + "，删除 " + result.Deleted + "，失败 " + result.Failed + "。");
        }

        private void EmptyRecycle()
        {
            long items;
            long size = RecycleBinCleaner.QuerySize(out items);
            if (size <= 0 && items <= 0)
            {
                StatusMessage = "回收站已经是空的。";
                AppendLog("回收站已为空。");
                ScanRecycle();
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                "即将清空回收站（" + MemoryStatus.Format((ulong)Math.Max(0L, size)) + "，" + items
                + " 项），此操作不可撤销。是否继续？",
                "清空回收站", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.OK)
            {
                AppendLog("用户取消了清空回收站。");
                return;
            }

            string error;
            if (RecycleBinCleaner.Empty(out error))
            {
                StatusMessage = "回收站已清空。";
                AppendLog("回收站已清空。");
            }
            else
            {
                StatusMessage = "清空回收站失败：" + error;
                AppendLog("清空回收站失败：" + error);
            }
            ScanRecycle();
        }

        private void OptimizeNetwork()
        {
            MessageBoxResult answer = MessageBox.Show(
                "网络优化将执行：刷新 DNS 缓存、重置 Winsock、重置 TCP/IP。\n"
                + "其中“重置 Winsock / TCP-IP”可能需要重启电脑才完全生效，安装过网络过滤组件的软件需重新注册。\n"
                + "是否继续？",
                "网络优化", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.OK)
            {
                AppendLog("用户取消了网络优化。");
                return;
            }

            DoNetwork(true, true, true);
        }

        private void DoNetwork(bool dns, bool winsock, bool tcpip)
        {
            string output;
            int code;

            if (dns)
            {
                code = NetworkOptimizer.FlushDns(out output);
                AppendLog((code == 0 ? "OK  " : "失败 ") + "刷新 DNS 缓存：" + NetworkOptimizer.FirstLine(output));
            }

            if (winsock)
            {
                code = NetworkOptimizer.ResetWinsock(out output);
                AppendLog((code == 0 ? "OK  " : "失败 ") + "重置 Winsock：" + NetworkOptimizer.FirstLine(output));
            }

            if (tcpip)
            {
                code = NetworkOptimizer.ResetTcpIp(out output);
                AppendLog((code == 0 ? "OK  " : "失败 ") + "重置 TCP/IP：" + NetworkOptimizer.FirstLine(output));
            }

            StatusMessage = "网络优化完成" + (winsock || tcpip ? "，重置操作可能需要重启电脑生效。" : "。");
        }

        private void OptimizeAll()
        {
            MessageBoxResult answer = MessageBox.Show(
                "一键优化将依次执行：内存整理 → 清理临时文件 → 清空回收站 → 刷新 DNS 缓存。\n"
                + "出于安全考虑，不会自动执行“重置 Winsock / TCP-IP”（如需请单独点“网络优化”并确认）。\n"
                + "是否继续？",
                "一键优化", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK)
            {
                AppendLog("用户取消了一键优化。");
                return;
            }

            AppendLog("=== 一键优化开始 ===");
            OptimizeMemory();
            CleanTempDirect();
            EmptyRecycleDirect();
            DoNetwork(true, false, false);
            ScanTemp();
            ScanRecycle();
            StatusMessage = "一键优化完成（已跳过重置网络协议栈）。详见日志。";
            AppendLog("=== 一键优化结束 ===");
        }

        private void CleanTempDirect()
        {
            CleanResult result = TempCleaner.Clean(TempCleaner.DefaultTargets(), AppendLog);
            AppendLog("临时文件清理：释放 " + result.FreedText + "，删除 " + result.Deleted + "，失败 " + result.Failed + "。");
        }

        private void EmptyRecycleDirect()
        {
            long items;
            long size = RecycleBinCleaner.QuerySize(out items);
            if (size <= 0 && items <= 0)
            {
                AppendLog("回收站已为空，跳过。");
                return;
            }

            string error;
            if (RecycleBinCleaner.Empty(out error))
            {
                AppendLog("回收站已清空（" + MemoryStatus.Format((ulong)Math.Max(0L, size)) + "）。");
            }
            else
            {
                AppendLog("清空回收站失败：" + error);
            }
        }
    }
}
