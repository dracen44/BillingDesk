using System.Windows;
using System.Windows.Data;
using System.Collections;

namespace BillingDesk.Converters
{
    /// <summary>
    /// Converts collection count to visibility. Returns Visible if count == 0 (empty state message),
    /// Collapsed if count > 0 (has items). Can be inverted with ConverterParameter="Inverted".
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool inverted = parameter != null && parameter.ToString() == "Inverted";

            if (value is ICollection collection)
            {
                bool isEmpty = collection.Count == 0;
                return (isEmpty ^ inverted) ? Visibility.Visible : Visibility.Collapsed;
            }
            if (value is int count)
            {
                bool isEmpty = count == 0;
                return (isEmpty ^ inverted) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
