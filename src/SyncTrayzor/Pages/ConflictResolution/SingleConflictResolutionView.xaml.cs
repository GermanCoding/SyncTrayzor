using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SyncTrayzor.Pages.ConflictResolution
{
    public partial class SingleConflictResolutionView
    {
        private ScrollViewer _optionsScrollViewer;
        private bool _isSyncingHorizontalScroll;

        public SingleConflictResolutionView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            HookHorizontalScrollSync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnhookHorizontalScrollSync();
        }

        private void HookHorizontalScrollSync()
        {
            UnhookHorizontalScrollSync();

            SelectConflictOptions.ApplyTemplate();

            _optionsScrollViewer =
                SelectConflictOptions.Template?.FindName("PART_ScrollViewer", SelectConflictOptions) as ScrollViewer ??
                FindDescendant<ScrollViewer>(SelectConflictOptions);

            if (_optionsScrollViewer == null)
                return;

            DetailsScrollViewer.ScrollChanged += DetailsScrollViewerOnScrollChanged;
            _optionsScrollViewer.ScrollChanged += OptionsScrollViewerOnScrollChanged;

            DetailsScrollViewer.ScrollToHorizontalOffset(_optionsScrollViewer.HorizontalOffset);
        }

        private void UnhookHorizontalScrollSync()
        {
            DetailsScrollViewer.ScrollChanged -= DetailsScrollViewerOnScrollChanged;

            if (_optionsScrollViewer != null)
            {
                _optionsScrollViewer.ScrollChanged -= OptionsScrollViewerOnScrollChanged;
                _optionsScrollViewer = null;
            }
        }

        private void DetailsScrollViewerOnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingHorizontalScroll || _optionsScrollViewer == null || e.HorizontalChange == 0)
                return;

            _isSyncingHorizontalScroll = true;
            _optionsScrollViewer.ScrollToHorizontalOffset(DetailsScrollViewer.HorizontalOffset);
            _isSyncingHorizontalScroll = false;
        }

        private void OptionsScrollViewerOnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingHorizontalScroll || e.HorizontalChange == 0)
                return;

            _isSyncingHorizontalScroll = true;
            DetailsScrollViewer.ScrollToHorizontalOffset(_optionsScrollViewer.HorizontalOffset);
            _isSyncingHorizontalScroll = false;
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
                return null;

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typedChild)
                    return typedChild;

                var descendant = FindDescendant<T>(child);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }
    }
}

