using System;
using System.Windows;
using System.Windows.Controls;

namespace ArkPlotWpf.Styles.Properties;

public static class AutoScroll
{
    private static bool _autoScroll;

    /// <summary>
    /// Gets auto scroll property.
    /// </summary>
    /// <param name="obj">The <see cref="DependencyObject"/> instance.</param>
    /// <returns>The property value.</returns>
    public static bool GetAutoScroll(DependencyObject obj)
    {
        return (bool)obj.GetValue(AutoScrollProperty);
    }

    /// <summary>
    /// Sets auto scroll property.
    /// </summary>
    /// <param name="obj">The <see cref="DependencyObject"/> instance.</param>
    /// <param name="value">The new property value.</param>
    public static void SetAutoScroll(DependencyObject obj, bool value)
    {
        obj.SetValue(AutoScrollProperty, value);
    }

    /// <summary>
    /// The auto scroll property.
    /// </summary>
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.RegisterAttached("AutoScroll", typeof(bool), typeof(AutoScroll), new PropertyMetadata(false, AutoScrollPropertyChanged));

    private static void AutoScrollPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer)
        {
            bool alwaysScrollToEnd = (e.NewValue != null) && (bool)e.NewValue;
            if (alwaysScrollToEnd)
            {
                scrollViewer.ScrollToEnd();
                scrollViewer.ScrollChanged += ScrollChanged;
            }
            else
            {
                scrollViewer.ScrollChanged -= ScrollChanged;
            }
        }
        else
        {
            throw new InvalidOperationException("The attached AlwaysScrollToEnd property can only be applied to ScrollViewer instances.");
        }
    }

    private static void ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!(sender is ScrollViewer scroll))
        {
            throw new InvalidOperationException("The attached AlwaysScrollToEnd property can only be applied to ScrollViewer instances.");
        }

        if (e.ExtentHeightChange == 0)
        {
            _autoScroll = Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 1e-6;
        }

        if (_autoScroll && e.ExtentHeightChange != 0)
        {
            scroll.ScrollToVerticalOffset(scroll.ExtentHeight);
        }
    }
}
