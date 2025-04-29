﻿using System;
using System.Windows;
using System.Windows.Data;

namespace SyncTrayzor.Xaml
{
    public class GridLengthToAbsoluteConverter : IValueConverter
    {
        public static readonly GridLengthToAbsoluteConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;

            try
            {
                return new GridLength(System.Convert.ToDouble(value));
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (!(value is GridLength))
                return null;

            return ((GridLength)value).Value;
        }
    }
}
