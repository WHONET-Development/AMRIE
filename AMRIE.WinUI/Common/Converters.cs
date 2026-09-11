using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AMRIE.WinUI.Common;

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int intVal)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out int targetVal))
            {
                return intVal == targetVal ? Visibility.Visible : Visibility.Collapsed;
            }
            return intVal > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
