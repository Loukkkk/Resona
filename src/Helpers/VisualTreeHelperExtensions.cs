using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Resona.Helpers
{
    public static class VisualTreeHelperExtensions
    {
        public static T FindVisualChild<T>(DependencyObject parent, string name = null) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && (name == null || element.Name == name))
                {
                    return element;
                }
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}