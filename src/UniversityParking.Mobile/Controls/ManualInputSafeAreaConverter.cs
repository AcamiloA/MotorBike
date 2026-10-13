using System.Globalization;

namespace UniversityParking.Mobile.Controls;

internal sealed class ManualInputSafeAreaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? SafeAreaEdges.All : new SafeAreaEdges(SafeAreaRegions.Container);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
