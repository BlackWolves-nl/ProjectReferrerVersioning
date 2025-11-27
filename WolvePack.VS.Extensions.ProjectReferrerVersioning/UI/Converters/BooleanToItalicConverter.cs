using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI.Converters;

public class BooleanToItalicConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isExcluded = value is bool b && b;
        return isExcluded ? FontStyles.Italic : FontStyles.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is FontStyle style && style == FontStyles.Italic;
    }
}
