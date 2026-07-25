using Avalonia.Data.Converters;
using GDMENUOrganizer.Core;
using System;
using System.Globalization;

namespace GDMENUOrganizer.Converter
{
    public class LocationEnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((LocationEnum)value).GetEnumName();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
