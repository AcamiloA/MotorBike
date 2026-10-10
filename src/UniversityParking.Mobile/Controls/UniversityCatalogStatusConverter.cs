using System.Globalization;
using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Controls;
public sealed class UniversityCatalogStatusConverter : IMultiValueConverter
{
    public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture) =>
        UniversityCatalogPresentation.Notice(values.ElementAtOrDefault(0) is true,values.ElementAtOrDefault(1) is true,
            values.ElementAtOrDefault(2) is int count ? count : 0,values.ElementAtOrDefault(3) as string);
    public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture) => throw new NotSupportedException();
}
