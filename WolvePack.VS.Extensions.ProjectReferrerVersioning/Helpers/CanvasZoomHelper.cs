using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers
{
    public static class CanvasZoomHelper
    {
        public static void Attach(Canvas canvas, double initialZoom = 1.0, double minZoom = 0.2, double maxZoom = 2.5)
        {
            ScaleTransform transform = new ScaleTransform(initialZoom, initialZoom);
            canvas.LayoutTransform = transform;
            double zoom = initialZoom;

            void zoomHandler(object s, MouseWheelEventArgs e)
            {
                if((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    if(e.Delta > 0) zoom = System.Math.Min(maxZoom, zoom * 1.1);
                    else zoom = System.Math.Max(minZoom, zoom / 1.1);
                    transform.ScaleX = zoom;
                    transform.ScaleY = zoom;
                    e.Handled = true;
                }
            }

            // Attach to canvas
            canvas.MouseWheel += zoomHandler;

            // Attach to parent ScrollViewer if available
            canvas.Loaded += (s, e) =>
            {
                DependencyObject parent = VisualTreeHelper.GetParent(canvas);
                while (parent != null && !(parent is ScrollViewer))
                    parent = VisualTreeHelper.GetParent(parent);
                if (parent is ScrollViewer scrollViewer)
                {
                    scrollViewer.PreviewMouseWheel += zoomHandler;
                }
            };
        }
    }
}
