using Microsoft.UI.Xaml.Data;

namespace GcToolkit.Converters;

public sealed class StringVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrEmpty(value?.ToString()) == Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
