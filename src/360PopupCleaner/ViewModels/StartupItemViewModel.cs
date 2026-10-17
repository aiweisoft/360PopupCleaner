using System;
using PopupCleaner.Models;

namespace PopupCleaner.ViewModels
{
    /// <summary>单个开机启动项的行视图模型。</summary>
    public sealed class StartupItemViewModel : ObservableObject
    {
        private readonly Action<string> _log;
        private bool _enabled;

        public StartupItemViewModel(StartupItem item, Action<string> log)
        {
            Item = item;
            _log = log;
            _enabled = item.IsEnabled;
            ToggleCommand = new RelayCommand(Toggle);
        }

        public StartupItem Item { get; private set; }
        public RelayCommand ToggleCommand { get; private set; }

        public string Name { get { return Item.Name; } }
        public string Location { get { return Item.LocationLabel; } }
        public string Command { get { return Item.Command; } }

        public bool Enabled { get { return _enabled; } }
        public string StateText { get { return _enabled ? "已启用" : "已禁用"; } }
        public string ActionText { get { return _enabled ? "禁用" : "启用"; } }

        private void Toggle()
        {
            bool target = !_enabled;
            string error;

            if (StartupManager.SetEnabled(Item, target, out error))
            {
                Item.IsEnabled = target;
                _enabled = target;
                OnPropertyChanged("Enabled");
                OnPropertyChanged("StateText");
                OnPropertyChanged("ActionText");

                if (_log != null)
                {
                    _log("启动项 " + Item.Name + " 已" + (target ? "启用" : "禁用") + "。");
                }
            }
            else if (_log != null)
            {
                _log("启动项 " + Item.Name + " 操作失败：" + error);
            }
        }
    }
}
