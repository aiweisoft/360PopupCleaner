using System.Collections.Specialized;
using System.Windows;
using PopupCleaner.ViewModels;

namespace PopupCleaner
{
    public partial class SystemOptimizeWindow : Window
    {
        private readonly SystemOptimizeViewModel _viewModel;

        public SystemOptimizeWindow()
        {
            InitializeComponent();

            _viewModel = new SystemOptimizeViewModel();
            DataContext = _viewModel;

            _viewModel.Log.CollectionChanged += OnLogChanged;
        }

        private void OnLogChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogScroll != null)
            {
                LogScroll.ScrollToEnd();
            }
        }
    }
}
