using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PopupCleaner.Converters
{
    /// <summary>Boolean to Visibility converter with an optional Invert flag.</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool && (bool)value;
            if (Invert)
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool visible = value is Visibility && (Visibility)value == Visibility.Visible;
            return Invert ? !visible : visible;
        }
    }
}
