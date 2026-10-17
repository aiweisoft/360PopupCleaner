using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using PopupCleaner.ViewModels;

namespace PopupCleaner
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
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

        private void OnOpenSystemOptimize(object sender, RoutedEventArgs e)
        {
            var window = new SystemOptimizeWindow { Owner = this };
            window.Show();
        }
    }
}
