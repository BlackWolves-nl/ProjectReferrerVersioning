using System;
using System.Windows;
using System.Windows.Media;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.UI;

public partial class MainWindow
{
    private double _zoom = 1.0;
    private const double _zoomMin = 0.2;
    private const double _zoomMax = 2.5;

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(_zoom * 1.1);
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(_zoom / 1.1);
    }

    private void ZoomFitButton_Click(object sender, RoutedEventArgs e)
    {
        // Fit the canvas to the ScrollViewer viewport
        ReferrerTreeCanvas.UpdateLayout();
        ReferrerTreeScrollViewer.UpdateLayout();

        double canvasWidth = ReferrerTreeCanvas.ActualWidth;
        double canvasHeight = ReferrerTreeCanvas.ActualHeight;
        double viewportWidth = ReferrerTreeScrollViewer.ViewportWidth;
        double viewportHeight = ReferrerTreeScrollViewer.ViewportHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            return;

        double scaleX = viewportWidth / canvasWidth;
        double scaleY = viewportHeight / canvasHeight;
        double fitScale = Math.Min(scaleX, scaleY);

        SetZoom(fitScale);
    }

    private void SetZoom(double zoom)
    {
        _zoom = Math.Max(_zoomMin, Math.Min(_zoomMax, zoom));
        ReferrerTreeCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
    }
}