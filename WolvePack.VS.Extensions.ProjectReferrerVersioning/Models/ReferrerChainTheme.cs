using System.Windows;
using System.Windows.Media;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Models;

public enum ReferrerChainThemeType
{
    Dark,
    Slate
}

public class ReferrerChainTheme
{
    public Brush NugetOrProjectChangeBrush { get; set; }       // NuGet/project reference change color
    public Brush NugetAndVersionChangeBrush { get; set; }      // NuGet + version change color
    public Brush VersionOnlyBrush { get; set; }                // Version only change color
    public Brush ModifiedBrush { get; set; }                   // General modified color
    public Brush CleanBrush { get; set; }                      // Clean (no changes) color
    public Brush VisitedBrush { get; set; }                    // Visited node color
    public Brush NodeBorderBrush { get; set; }                 // Node border color
    public Brush BackgroundBrush { get; set; }                 // Canvas background color
    public Brush TextBrush { get; set; }                       // Node text color
    public double NodeWidth { get; set; }
    public double NodeHeight { get; set; }
    public double HorizontalSpacing { get; set; }
    public double VerticalSpacing { get; set; }
    public string FontFamily { get; set; }
    public double FontSize { get; set; }
    public Brush ArrowBrush { get; set; }                      // Color for lines and arrowheads
    public Brush RootNodeBorderBrush { get; set; }              // Border for root nodes
    public Brush HoverBorderBrush { get; set; }                // Border for hovered nodes
    public FontWeight ProjectNameFontWeight { get; set; }      // Font weight for project name
    public FontWeight VersionFontWeight { get; set; }          // Font weight for version text
    public Brush BadgeBackgroundBrush { get; set; }             // Badge background color
    public Brush BadgeForegroundBrush { get; set; }             // Badge text color
    
    // Root badge border colors for different states
    public Brush RootBadgeExcludedBorderBrush { get; set; }          // White border for excluded projects
    public Brush RootBadgeRequiresVersionBorderBrush { get; set; }   // Orange border for requiring version selection
    public Brush RootBadgeVersionSelectedBorderBrush { get; set; }   // Green border for version selected
    
    // Additional theme colors for shadows, borders, and text measurement
    public Color ShadowColor { get; set; }                          // Color for all drop shadow effects
    public Brush BadgeBorderBrush { get; set; }                     // Border color for badges and pills
    public Brush SeparatorBrush { get; set; }                       // Background color for pill separators
    public Brush RootBadgeTextBrush { get; set; }                   // Text color for root badge ("R")
    public Brush FormattedTextBrush { get; set; }                   // Color for FormattedText measurements

    public static ReferrerChainTheme LoadThemeFromResources()
    {
        ResourceDictionary resources = Application.Current.Resources;
        return new ReferrerChainTheme
        {
            NugetOrProjectChangeBrush = resources["ProjectStatusNuGetOrProjectReferenceChangesBrush"] as Brush,
            NugetAndVersionChangeBrush = resources["ProjectStatusNuGetOrProjectReferenceAndVersionChangesBrush"] as Brush,
            VersionOnlyBrush = resources["ProjectStatusIsVersionChangeOnlyBrush"] as Brush,
            ModifiedBrush = resources["ProjectStatusModifiedBrush"] as Brush,
            CleanBrush = resources["ProjectStatusCleanBrush"] as Brush,
            VisitedBrush = resources["ExcludedProjectForegroundBrush"] as Brush,
            NodeBorderBrush = resources["BorderBrushColor"] as Brush,
            BackgroundBrush = resources["PrimaryBackgroundBrush"] as Brush,
            TextBrush = resources["PrimaryForegroundBrush"] as Brush,
            ArrowBrush = resources["AccentBrush"] as Brush,
            RootNodeBorderBrush = resources["RootNodeBorderBrush"] as Brush,
            HoverBorderBrush = resources["HoverBorderBrush"] as Brush,
            BadgeBackgroundBrush = resources["BadgeBackgroundBrush"] as Brush,
            BadgeForegroundBrush = resources["BadgeForegroundBrush"] as Brush,
            RootBadgeExcludedBorderBrush = resources["RootBadgeExcludedBorderBrush"] as Brush,
            RootBadgeRequiresVersionBorderBrush = resources["RootBadgeRequiresVersionBorderBrush"] as Brush,
            RootBadgeVersionSelectedBorderBrush = resources["RootBadgeVersionSelectedBorderBrush"] as Brush,
            ShadowColor = (Color)(resources["ShadowColor"] ?? Colors.Black),
            BadgeBorderBrush = resources["BadgeBorderBrush"] as Brush,
            SeparatorBrush = resources["SeparatorBrush"] as Brush,
            RootBadgeTextBrush = resources["RootBadgeTextBrush"] as Brush,
            FormattedTextBrush = resources["FormattedTextBrush"] as Brush,
            NodeWidth = 300,
            NodeHeight = 60,
            HorizontalSpacing = 25,
            VerticalSpacing = 15,
            FontFamily = "Segoe UI",
            FontSize = 14,
            ProjectNameFontWeight = FontWeights.SemiBold,
            VersionFontWeight = FontWeights.Normal
        };
    }
}
