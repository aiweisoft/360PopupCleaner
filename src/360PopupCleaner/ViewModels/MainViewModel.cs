using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PopupCleaner.Models;

namespace PopupCleaner.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly List<ToggleRowViewModel> _allRows = new List<ToggleRowViewModel>();

        private string _statusMessage;
        private string _detectSummary;
        private bool _is360Installed;
        private string _adProcessSummary;
        private bool _hasAdProcesses;

        public MainViewModel()
        {
            Rows = new ObservableCollection<ToggleRowViewModel>();
            Log = new ObservableCollection<string>();
            AdProcesses = new ObservableCollection<AdProcessItem>();

            foreach (ToggleDefinition definition in ToggleCatalog.CreateAll())
            {
                var row = new ToggleRowViewModel(definition);
                _allRows.Add(row);
                Rows.Add(row);
            }

            RefreshCommand = new RelayCommand(Refresh);
            CloseAllCommand = new RelayCommand(CloseAll);
            ApplyCommand = new RelayCommand(Apply);
            RestoreCommand = new RelayCommand(Restore);
            OpenBackupFolderCommand = new RelayCommand(OpenBackupFolder);
            KillAdProcessesCommand = new RelayCommand(KillAdProcesses);

            Refresh();
        }

        public ObservableCollection<ToggleRowViewModel> Rows { get; private set; }
        public ObservableCollection<string> Log { get; private set; }
        public ObservableCollection<AdProcessItem> AdProcesses { get; private set; }

        public RelayCommand RefreshCommand { get; private set; }
        public RelayCommand CloseAllCommand { get; private set; }
        public RelayCommand ApplyCommand { get; private set; }
        public RelayCommand RestoreCommand { get; private set; }
        public RelayCommand OpenBackupFolderCommand { get; private set; }
        public RelayCommand KillAdProcessesCommand { get; private set; }

        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        public string DetectSummary
        {
            get { return _detectSummary; }
            private set { SetProperty(ref _detectSummary, value); }
        }

        public bool Is360Installed
        {
            get { return _is360Installed; }
            private set { SetProperty(ref _is360Installed, value); }
        }

        public string AdProcessSummary
        {
            get { return _adProcessSummary; }
            private set { SetProperty(ref _adProcessSummary, value); }
        }

        public bool HasAdProcesses
        {
            get { return _hasAdProcesses; }
            private set { SetProperty(ref _hasAdProcesses, value); }
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
            DetectResult result = Detector.Detect();
            Is360Installed = result.Installed;

            string version = string.IsNullOrEmpty(result.Version) ? "" : " v" + result.Version;
            string processes = result.RunningProcesses.Count == 0
                ? "未检测到运行中的 360 进程"
                : "运行中：" + string.Join("、", result.RunningProcesses);

            DetectSummary = (result.Installed ? "已安装 360 安全卫士" + version : "未检测到 360 安全卫士")
                            + "  ·  " + processes;

            foreach (ToggleRowViewModel row in _allRows)
            {
                row.LoadFromRegistry();
            }

            ScanAdProcesses();

            StatusMessage = "就绪。开关打开 = 关闭该广告 / 推送；改完点“应用更改”写盘（会先自动备份）。";
            AppendLog("已刷新 " + _allRows.Count + " 个开关的当前状态。");
        }

        private void ScanAdProcesses()
        {
            AdProcesses.Clear();
            IList<AdProcessItem> found = AdProcessScanner.Find();
            foreach (AdProcessItem item in found)
            {
                AdProcesses.Add(item);
            }

            HasAdProcesses = AdProcesses.Count > 0;
            AdProcessSummary = HasAdProcesses
                ? "检测到 " + AdProcesses.Count + " 个广告弹窗进程（开关只能阻止后续拉起，已在显示的弹窗需结束进程才会消失）"
                : "未检测到正在运行的广告弹窗进程";

            if (HasAdProcesses)
            {
                AppendLog("检测到广告弹窗进程：" + string.Join("、", AdProcesses.Select(a => a.Display)));
            }
        }

        private void KillAdProcesses()
        {
            IList<AdProcessItem> targets = AdProcessScanner.Find();
            if (targets.Count == 0)
            {
                ScanAdProcesses();
                StatusMessage = "没有正在运行的广告弹窗进程。";
                AppendLog("没有正在运行的广告弹窗进程。");
                return;
            }

            int ok = 0;
            int fail = 0;

            foreach (AdProcessItem item in targets)
            {
                string error;
                if (AdProcessScanner.Kill(item.Id, out error))
                {
                    ok++;
                    AppendLog("OK  已结束 " + item.Name + " (PID " + item.Id + ")");
                }
                else
                {
                    fail++;
                    AppendLog("失败 结束 " + item.Name + " (PID " + item.Id + ")：" + error);
                }
            }

            ScanAdProcesses();

            StatusMessage = "已结束广告弹窗进程：成功 " + ok + "，失败 " + fail + "。"
                            + (HasAdProcesses ? "仍有进程在运行，可再次点击。" : "已全部关闭。");
            AppendLog("结束广告弹窗进程完成：成功 " + ok + "，失败 " + fail + "。");
        }

        private void CloseAll()
        {
            foreach (ToggleRowViewModel row in _allRows)
            {
                row.IsOff = true;
            }

            AppendLog("已把全部 " + _allRows.Count + " 个开关切到“关闭”，正在应用…");
            Apply();
        }

        private void Apply()
        {
            List<ToggleRowViewModel> dirty = _allRows.Where(r => r.IsDirty).ToList();
            if (dirty.Count == 0)
            {
                StatusMessage = "没有需要应用的更改。";
                AppendLog("没有需要应用的更改。");
                return;
            }

            // 1) 先把将被改动的原值（含可选的时间戳值）全部备份。
            var snapshots = new List<RegValueSnapshot>();
            foreach (ToggleRowViewModel row in dirty)
            {
                snapshots.Add(RegistryAccess.Capture(row.Definition.SubKey, row.Definition.ValueName));
                if (row.Definition.CloseTimeValueName != null)
                {
                    snapshots.Add(RegistryAccess.Capture(row.Definition.SubKey, row.Definition.CloseTimeValueName));
                }
            }

            string backupError;
            string backupPath = BackupManager.Save(snapshots, out backupError);
            if (backupPath == null)
            {
                StatusMessage = "备份失败，已中止，未做任何更改。";
                AppendLog("备份失败，已中止：" + backupError);
                return;
            }

            AppendLog("已备份 " + snapshots.Count + " 个原值 -> " + backupPath);

            // 2) 写入新值。
            int ok = 0;
            int fail = 0;
            int now = RegistryAccess.UnixNow();

            foreach (ToggleRowViewModel row in dirty)
            {
                int target = row.IsOff ? row.Definition.DisableValue : row.Definition.EnableValue;
                string error;

                if (RegistryAccess.WriteDword(row.Definition, target, out error))
                {
                    ok++;
                    if (row.IsOff && row.Definition.CloseTimeValueName != null)
                    {
                        string closeTimeError;
                        RegistryAccess.WriteDwordValue(
                            row.Definition.SubKey, row.Definition.CloseTimeValueName, now, out closeTimeError);
                    }

                    AppendLog("OK  " + row.Title + " -> " + target);
                }
                else
                {
                    fail++;
                    AppendLog("失败 " + row.Title + "：" + error);
                }
            }

            foreach (ToggleRowViewModel row in _allRows)
            {
                row.LoadFromRegistry();
            }

            StatusMessage = "已应用 " + ok + " 项" + (fail > 0 ? "，" + fail + " 项失败" : "") + "。备份：" + backupPath;
            AppendLog("应用完成：成功 " + ok + "，失败 " + fail + "。");
        }

        private void Restore()
        {
            string path = BackupManager.LatestBackupPath();
            if (path == null)
            {
                StatusMessage = "没有找到可还原的备份。";
                AppendLog("没有找到可还原的备份。");
                return;
            }

            string loadError;
            IList<RegValueSnapshot> snapshots = BackupManager.Load(path, out loadError);
            if (snapshots == null)
            {
                StatusMessage = "读取备份失败。";
                AppendLog("读取备份失败：" + loadError);
                return;
            }

            int ok = 0;
            int fail = 0;

            foreach (RegValueSnapshot snapshot in snapshots)
            {
                string error;
                if (RegistryAccess.Apply(snapshot, out error))
                {
                    ok++;
                }
                else
                {
                    fail++;
                    AppendLog("失败 还原 " + snapshot.Name + "：" + error);
                }
            }

            foreach (ToggleRowViewModel row in _allRows)
            {
                row.LoadFromRegistry();
            }

            StatusMessage = "已从备份还原 " + ok + " 项" + (fail > 0 ? "，" + fail + " 项失败" : "") + "。来源：" + path;
            AppendLog("还原完成（" + path + "）：成功 " + ok + "，失败 " + fail + "。");
        }

        private void OpenBackupFolder()
        {
            try
            {
                Directory.CreateDirectory(BackupManager.BackupDir);
                Process.Start("explorer.exe", BackupManager.BackupDir);
            }
            catch (Exception ex)
            {
                AppendLog("打开备份目录失败：" + ex.Message);
            }
        }
    }
}
