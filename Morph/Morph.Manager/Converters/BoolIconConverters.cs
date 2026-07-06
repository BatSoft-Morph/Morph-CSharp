using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace Morph.Manager.Converters
{
    /// <summary>Renders a boolean as a tick (true) or a cross (false).</summary>
    public class BoolToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool flag && flag) ? "✓" : "✗";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Colours a boolean icon green (true) or red (false).</summary>
    public class BoolToIconColourConverter : IValueConverter
    {
        private static readonly Color s_positive = Color.FromArgb("#2E7D32");
        private static readonly Color s_negative = Color.FromArgb("#C62828");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool flag && flag) ? s_positive : s_negative;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
