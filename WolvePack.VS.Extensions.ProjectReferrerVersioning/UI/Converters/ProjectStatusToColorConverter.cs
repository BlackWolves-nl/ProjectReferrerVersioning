using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

using WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI.Converters
{
    /// <summary>
    /// Converts ProjectStatus enum to color brush for UI display
    /// </summary>
    public class ProjectStatusToColorConverter : IValueConverter
    {
        public static readonly ProjectStatusToColorConverter Instance = new ProjectStatusToColorConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProjectStatus status)
            {
                switch (status)
                {
                    case ProjectStatus.Initial:
                        return Application.Current.Resources["ProjectStatusInitialBrush"] as SolidColorBrush ?? Brushes.LightGray;
                    case ProjectStatus.Modified:
                        return Application.Current.Resources["ProjectStatusModifiedBrush"] as SolidColorBrush ?? Brushes.Red;
                    case ProjectStatus.NuGetOrProjectReferenceChanges:
                        return Application.Current.Resources["ProjectStatusNuGetOrProjectReferenceChangesBrush"] as SolidColorBrush ?? Brushes.Orange;
                    case ProjectStatus.NuGetOrProjectReferenceAndVersionChanges:
                        return Application.Current.Resources["ProjectStatusNuGetOrProjectReferenceAndVersionChangesBrush"] as SolidColorBrush ?? Brushes.DarkSlateBlue;
                    case ProjectStatus.IsVersionChangeOnly:
                        return Application.Current.Resources["ProjectStatusIsVersionChangeOnlyBrush"] as SolidColorBrush ?? Brushes.SteelBlue;
                    case ProjectStatus.Clean:
                    default:
                        return Application.Current.Resources["ProjectStatusCleanBrush"] as SolidColorBrush ?? Brushes.LightGreen;
                }
            }

            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for ProjectStatusToColorConverter");
        }
    }
}