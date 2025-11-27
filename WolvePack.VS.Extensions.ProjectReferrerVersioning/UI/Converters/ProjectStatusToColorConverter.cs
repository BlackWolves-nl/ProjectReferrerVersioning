using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI.Converters;

/// <summary>
/// Converts ProjectStatus enum to color brush for UI display
/// </summary>
public class ProjectStatusToColorConverter : IValueConverter
{
    public static readonly ProjectStatusToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ProjectStatus status)
        {
            return status switch
            {
                ProjectStatus.Initial => Application.Current.Resources["ProjectStatusInitialBrush"] as SolidColorBrush ?? Brushes.LightGray,
                ProjectStatus.Modified => Application.Current.Resources["ProjectStatusModifiedBrush"] as SolidColorBrush ?? Brushes.Red,
                ProjectStatus.NuGetOrProjectReferenceChanges => Application.Current.Resources["ProjectStatusNuGetOrProjectReferenceChangesBrush"] as SolidColorBrush ?? Brushes.Orange,
                ProjectStatus.NuGetOrProjectReferenceAndVersionChanges => Application.Current.Resources["ProjectStatusNuGetOrProjectReferenceAndVersionChangesBrush"] as SolidColorBrush ?? Brushes.DarkSlateBlue,
                ProjectStatus.IsVersionChangeOnly => Application.Current.Resources["ProjectStatusIsVersionChangeOnlyBrush"] as SolidColorBrush ?? Brushes.SteelBlue,
                _ => Application.Current.Resources["ProjectStatusCleanBrush"] as SolidColorBrush ?? Brushes.LightGreen,
            };
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("ConvertBack is not supported for ProjectStatusToColorConverter");
    }
}