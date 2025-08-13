using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI.Converters
{
    public class ExcludedProjectForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isExcluded = value is bool b && b;
            SolidColorBrush dullGray = Application.Current.Resources["ExcludedProjectForegroundBrush"] as SolidColorBrush;
            SolidColorBrush normal = Application.Current.Resources["PrimaryForegroundBrush"] as SolidColorBrush;
            return isExcluded ? dullGray ?? Brushes.Gray : normal ?? Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
