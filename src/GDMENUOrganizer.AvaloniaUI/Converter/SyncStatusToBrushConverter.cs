using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GDMENUOrganizer.Core.Database;
using System;
using System.Globalization;

namespace GDMENUOrganizer.Converter
{
    public class SyncStatusToBrushConverter : IValueConverter
    {
        private static readonly IBrush MissingBrush = Brush.Parse("#E53935");
        private static readonly IBrush NewBrush = Brush.Parse("#43A047");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = LibrarySyncStatuses.Normalize(value as string);
            return status switch
            {
                LibrarySyncStatuses.Missing => MissingBrush,
                LibrarySyncStatuses.New => NewBrush,
                _ => AvaloniaProperty.UnsetValue
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
