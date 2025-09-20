using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

using Stylet;

namespace SyncTrayzor.Xaml
{
    public class PopupConductorBehaviour : DetachingBehaviour<Popup>
    {
        private Point? dragStart;

        public object DataContext
        {
            get => GetValue(DataContextProperty);
            set => SetValue(DataContextProperty, value);
        }

        public static readonly DependencyProperty DataContextProperty =
            DependencyProperty.Register("DataContext", typeof(object), typeof(PopupConductorBehaviour), new PropertyMetadata(null));

        protected override void AttachHandlers()
        {
            AssociatedObject.Opened += Opened;
            AssociatedObject.Closed += Closed;
            AssociatedObject.PreviewMouseUp += ManualClose;
            AssociatedObject.MouseMove += MouseMove;
            AssociatedObject.MouseLeave += MouseLeave;
        }

        protected override void DetachHandlers()
        {
            AssociatedObject.Opened -= Opened;
            AssociatedObject.Closed -= Closed;
            AssociatedObject.PreviewMouseUp -= ManualClose;
            AssociatedObject.MouseMove -= MouseMove;
            AssociatedObject.MouseLeave -= MouseLeave;
        }

        private void Opened(object sender, EventArgs e)
        {
            if (DataContext is IScreenState screenState)
                screenState.Activate();
        }

        private void Closed(object sender, EventArgs e)
        {
            if (DataContext is IScreenState screenState)
                screenState.Close();
        }

        private void ManualClose(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                AssociatedObject.IsOpen = false;
            }
        }

        private void MouseLeave(object sender, MouseEventArgs e)
        {
            dragStart = null; // mouse left the popup, stop dragging
        }

        private void MouseMove(object sender, MouseEventArgs e)
        {
            // we need a FrameworkElement to get ActualWidth/Height
            if (AssociatedObject.Child is not FrameworkElement child)
            {
                return;
            }

            if (e.LeftButton == MouseButtonState.Released)
            {
                dragStart = null; // mouse released, we're not dragging
                return;
            }

            var currentPos = e.GetPosition(child);

            if (dragStart is null)
            {
                // Capture the start point of the drag
                dragStart = currentPos;
                return;
            }

            // Move the popup by the delta
            var delta = currentPos - dragStart.Value;

            var newHorizontalOffset = AssociatedObject.HorizontalOffset + delta.X;
            var newVerticalOffset = AssociatedObject.VerticalOffset + delta.Y;

            // Constrain to screen bounds
            var minOffsetX = SystemParameters.WorkArea.Left;
            if (newHorizontalOffset < minOffsetX)
            {
                newHorizontalOffset = minOffsetX;
            }

            var maxOffsetX = SystemParameters.WorkArea.Width - child.ActualWidth;
            if (newHorizontalOffset > maxOffsetX)
            {
                newHorizontalOffset = maxOffsetX;
            }

            var minOffsetY = SystemParameters.WorkArea.Top;
            if (newVerticalOffset < minOffsetY)
            {
                newVerticalOffset = minOffsetY;
            }

            var maxOffsetY = SystemParameters.WorkArea.Height - child.ActualHeight;
            if (newVerticalOffset > maxOffsetY)
            {
                newVerticalOffset = maxOffsetY;
            }

            // Set the new offsets
            AssociatedObject.HorizontalOffset = newHorizontalOffset;
            AssociatedObject.VerticalOffset = newVerticalOffset;
        }
    }
}
