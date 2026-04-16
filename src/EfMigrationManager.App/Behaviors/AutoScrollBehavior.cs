namespace EfMigrationManager.App.Behaviors;

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

public sealed class AutoScrollBehavior : Behavior<ScrollViewer>
{
    private ItemsControl? _itemsControl;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _itemsControl = AssociatedObject.Content as ItemsControl;
        if (_itemsControl?.ItemsSource is INotifyCollectionChanged observable)
            observable.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            AssociatedObject.ScrollToEnd();
    }

    protected override void OnDetaching()
    {
        if (_itemsControl?.ItemsSource is INotifyCollectionChanged observable)
            observable.CollectionChanged -= OnCollectionChanged;
        base.OnDetaching();
    }
}
