using PopupCleaner.Models;

namespace PopupCleaner.ViewModels
{
    /// <summary>单个开关的行视图模型。IsOff == true 表示“关闭该广告 / 推送”。</summary>
    public sealed class ToggleRowViewModel : ObservableObject
    {
        private bool _desiredOff;
        private bool _currentOff;
        private bool _exists;
        private int _rawValue;

        public ToggleRowViewModel(ToggleDefinition definition)
        {
            Definition = definition;
        }

        public ToggleDefinition Definition { get; private set; }

        public string Title { get { return Definition.Title; } }
        public string Group { get { return Definition.Group; } }
        public string Description { get { return Definition.Description; } }
        public string Source { get { return Definition.Source; } }
        public string RegistryPath { get { return Definition.FullValuePath; } }

        public bool Exists { get { return _exists; } }
        public int RawValue { get { return _rawValue; } }
        public bool CurrentIsOff { get { return _currentOff; } }

        /// <summary>用户想让它处于“关闭”状态。绑定到开关。</summary>
        public bool IsOff
        {
            get { return _desiredOff; }
            set
            {
                if (SetProperty(ref _desiredOff, value))
                {
                    OnPropertyChanged("IsDirty");
                    OnPropertyChanged("StateText");
                }
            }
        }

        public bool IsDirty { get { return _desiredOff != _currentOff; } }

        public string ValueText
        {
            get { return _exists ? _rawValue.ToString() : "不存在"; }
        }

        public string StateText
        {
            get
            {
                if (IsDirty)
                {
                    return "待应用";
                }

                if (!_exists)
                {
                    return "未设置";
                }

                return _currentOff ? "已关闭" : "已开启";
            }
        }

        /// <summary>从注册表重新读取当前值，并把期望状态同步为当前状态。</summary>
        public void LoadFromRegistry()
        {
            int value;
            _exists = RegistryAccess.TryReadInt(Definition, out value);
            _rawValue = value;
            _currentOff = _exists && value == Definition.DisableValue;
            _desiredOff = _currentOff;

            OnPropertyChanged("Exists");
            OnPropertyChanged("RawValue");
            OnPropertyChanged("ValueText");
            OnPropertyChanged("CurrentIsOff");
            OnPropertyChanged("IsOff");
            OnPropertyChanged("IsDirty");
            OnPropertyChanged("StateText");
        }
    }
}
