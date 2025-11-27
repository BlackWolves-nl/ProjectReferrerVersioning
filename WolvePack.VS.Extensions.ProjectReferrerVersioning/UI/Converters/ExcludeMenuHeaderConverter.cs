using System;
using System.Globalization;
using System.Windows.Data;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI.Converters;

public class ExcludeMenuHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isExcluded = value is bool b && b;
        return isExcluded ? "Include in version updates" : "Exclude from version updates";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
