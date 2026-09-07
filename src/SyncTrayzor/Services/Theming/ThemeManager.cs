using Microsoft.Win32;
using NLog;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace SyncTrayzor.Services.Theming
{
    public interface IThemeManager : IDisposable
    {
        ApplicationTheme SelectedTheme { get; }
        bool IsDarkTheme { get; }
        void Apply(ApplicationTheme theme);
    }

    public sealed class ThemeManager : IThemeManager
    {
        private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AppsUseLightThemeValue = "AppsUseLightTheme";
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public ApplicationTheme SelectedTheme { get; private set; } = ApplicationTheme.System;
        public bool IsDarkTheme { get; private set; }

        public ThemeManager()
        {
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(WindowLoaded));
            SystemEvents.UserPreferenceChanged += UserPreferenceChanged;
        }

        public void Apply(ApplicationTheme theme)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => Apply(theme));
                return;
            }

            SelectedTheme = theme;
            IsDarkTheme = theme == ApplicationTheme.Dark ||
                          (theme == ApplicationTheme.System && SystemUsesDarkTheme());

            ApplyPalette(IsDarkTheme);

            foreach (Window window in Application.Current.Windows)
                ApplyWindowTheme(window);
        }

        private static void ApplyPalette(bool dark)
        {
            var resources = Application.Current.Resources;

            var background = ColorFrom(dark ? "#1E1E1E" : "#FFFFFF");
            var surface = ColorFrom(dark ? "#252526" : "#F5F5F5");
            var control = ColorFrom(dark ? "#2D2D30" : "#FFFFFF");
            var foreground = ColorFrom(dark ? "#F3F3F3" : "#1B1B1B");
            var mutedForeground = ColorFrom(dark ? "#B8B8B8" : "#606060");
            var border = ColorFrom(dark ? "#4A4A4A" : "#B8B8B8");
            var accent = ColorFrom(dark ? "#3EA6E8" : "#0078D4");
            var focusBorder = ColorFrom(dark ? "#707070" : "#686868");
            var hover = ColorFrom(dark ? "#3A3A3D" : "#E7E7E7");
            var pressed = ColorFrom(dark ? "#454549" : "#D8D8D8");
            var disabled = ColorFrom(dark ? "#858585" : "#707070");
            var scrollBarTrack = ColorFrom(dark ? "#252526" : "#E5E5E5");
            var scrollBarThumb = ColorFrom(dark ? "#8A8A8A" : "#858585");
            var scrollBarThumbHover = ColorFrom(dark ? "#A5A5A5" : "#686868");

            SetBrush(resources, "AppBackgroundBrush", background);
            SetBrush(resources, "AppSurfaceBrush", surface);
            SetBrush(resources, "AppControlBackgroundBrush", control);
            SetBrush(resources, "AppForegroundBrush", foreground);
            SetBrush(resources, "AppMutedForegroundBrush", mutedForeground);
            SetBrush(resources, "AppBorderBrush", border);
            SetBrush(resources, "AppAccentBrush", accent);
            SetBrush(resources, "AppFocusBorderBrush", focusBorder);
            SetBrush(resources, "AppHoverBrush", hover);
            SetBrush(resources, "AppPressedBrush", pressed);
            SetBrush(resources, "AppScrollBarTrackBrush", scrollBarTrack);
            SetBrush(resources, "AppScrollBarThumbBrush", scrollBarThumb);
            SetBrush(resources, "AppScrollBarThumbHoverBrush", scrollBarThumbHover);

            // Default WPF templates consume these system resource keys. Overriding them
            // keeps controls which do not have a SyncTrayzor-specific style in theme.
            SetBrush(resources, SystemColors.WindowBrushKey, background);
            SetBrush(resources, SystemColors.WindowTextBrushKey, foreground);
            SetBrush(resources, SystemColors.ControlBrushKey, control);
            SetBrush(resources, SystemColors.ControlTextBrushKey, foreground);
            SetBrush(resources, SystemColors.MenuBrushKey, surface);
            SetBrush(resources, SystemColors.MenuTextBrushKey, foreground);
            SetBrush(resources, SystemColors.HighlightBrushKey, accent);
            SetBrush(resources, SystemColors.HighlightTextBrushKey, Colors.White);
            SetBrush(resources, SystemColors.GrayTextBrushKey, disabled);
            SetBrush(resources, SystemColors.ActiveBorderBrushKey, border);
            SetBrush(resources, SystemColors.InactiveBorderBrushKey, border);
            SetBrush(resources, SystemColors.InactiveSelectionHighlightBrushKey, hover);
            SetBrush(resources, SystemColors.InactiveSelectionHighlightTextBrushKey, foreground);
        }

        private static void SetBrush(ResourceDictionary resources, object key, Color color)
        {
            resources[key] = new SolidColorBrush(color);
        }

        private static Color ColorFrom(string color)
        {
            return (Color)ColorConverter.ConvertFromString(color);
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
                ApplyWindowTheme(window);
        }

        private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (SelectedTheme == ApplicationTheme.System)
                Apply(ApplicationTheme.System);
        }

        private void ApplyWindowTheme(Window window)
        {
            window.SetResourceReference(Control.BackgroundProperty, "AppBackgroundBrush");
            window.SetResourceReference(Control.ForegroundProperty, "AppForegroundBrush");

            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
                return;

            var enabled = IsDarkTheme ? 1 : 0;
            if (DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }

        private static bool SystemUsesDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
                return key?.GetValue(AppsUseLightThemeValue) is int value && value == 0;
            }
            catch (Exception e)
            {
                logger.Warn(e, "Could not read the Windows application theme; using light theme");
                return false;
            }
        }

        public void Dispose()
        {
            SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
    }
}
