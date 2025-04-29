﻿using System;
using System.Windows.Data;

namespace SyncTrayzor.Xaml
{
    public class RemoveMnemonicsConverter : IValueConverter
    {
        public static RemoveMnemonicsConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var val = value as string;
            return val == null ? null : val.Replace("_", "__");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var val = value as string;
            if (val == null)
                return null;

            return val.Replace("__", "_");
        }
    }
}
