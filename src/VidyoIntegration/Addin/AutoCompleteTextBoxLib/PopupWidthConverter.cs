using System;
using System.Globalization;
using System.Windows.Data;

namespace AutoCompleteTextBoxLib
{
    public class PopupWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var val = ((double) value) - 10;
                return val > 0 ? val : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
