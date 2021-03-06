using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Waf.MusicManager.Presentation.Controls;

public static class SelectionBehavior
{
    private static readonly List<Tuple<IMultiSelector, INotifyCollectionChanged>> multiSelectorWithObservableList = new();
    private static readonly HashSet<object> syncListsThatAreUpdating = new();
    private static readonly HashSet<Selector> selectorsThatAreUpdating = new();

    public static readonly DependencyProperty SyncSelectedItemsProperty =
        DependencyProperty.RegisterAttached("SyncSelectedItems", typeof(IList), typeof(SelectionBehavior), new FrameworkPropertyMetadata(null, SyncSelectedItemsPropertyChanged));

    [AttachedPropertyBrowsableForType(typeof(Selector))]
    public static IList GetSyncSelectedItems(DependencyObject obj) => (IList)obj.GetValue(SyncSelectedItemsProperty);

    public static void SetSyncSelectedItems(DependencyObject obj, IList value) => obj.SetValue(SyncSelectedItemsProperty, value);

    private static void SyncSelectedItemsPropertyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Selector selector) throw new ArgumentException("The attached property SelectedItems can only be used with a Selector.", nameof(element));
        TryCleanUpOldItem(selector);
        try
        {
            var multiSelector = TryGetMultiSelector(selector);
            if (multiSelector == null) return;

            var list = GetSyncSelectedItems(selector);
            if (list == null) return;

            if (multiSelector.SelectedItems.Count > 0) multiSelector.SelectedItems.Clear();
            foreach (var x in list) multiSelector.SelectedItems.Add(x);

            if (list is not INotifyCollectionChanged observableList) return;

            multiSelectorWithObservableList.Add(Tuple.Create(multiSelector, observableList));
            CollectionChangedEventManager.AddHandler(observableList, ListCollectionChanged);
        }
        finally
        {
            selector.SelectionChanged += SelectorSelectionChanged;
        }
    }

    private static void TryCleanUpOldItem(Selector selector)
    {
        selector.SelectionChanged -= SelectorSelectionChanged;  // Remove a previously added event handler.

        var item = multiSelectorWithObservableList.FirstOrDefault(x => x.Item1.Selector == selector);
        if (item == null) return;

        multiSelectorWithObservableList.Remove(item);
        CollectionChangedEventManager.RemoveHandler(item.Item2, ListCollectionChanged);
    }


    private static void ListCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (syncListsThatAreUpdating.Contains(sender!)) return;

        var multiSelector = multiSelectorWithObservableList.First(x => x.Item2 == sender).Item1;
        selectorsThatAreUpdating.Add(multiSelector.Selector);
        try
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var x in e.NewItems!) multiSelector.SelectedItems.Add(x);
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var x in e.OldItems!) multiSelector.SelectedItems.Remove(x);
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                multiSelector.SelectedItems.Clear();
                foreach (var x in (IEnumerable)sender!) multiSelector.SelectedItems.Add(x);
            }
            else throw new NotSupportedException();
        }
        finally
        {
            selectorsThatAreUpdating.Remove(multiSelector.Selector);
        }
    }

    private static void SelectorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selector = (Selector)sender;
        if (selectorsThatAreUpdating.Contains(selector)) return;

        var list = GetSyncSelectedItems(selector);
        if (list == null) return;

        syncListsThatAreUpdating.Add(list);
        try
        {
            foreach (var x in e.RemovedItems) list.Remove(x);
            foreach (var x in e.AddedItems) list.Add(x);
        }
        finally
        {
            syncListsThatAreUpdating.Remove(list);
        }
    }

    private static IMultiSelector? TryGetMultiSelector(Selector selector) => selector switch
    {
        ListBox x => new ListBoxAdapter(x),
        MultiSelector y => new MultiSelectorAdapter(y),
        _ => null
    };


    private interface IMultiSelector
    {
        Selector Selector { get; }
            
        IList SelectedItems { get; }
    }

    private class ListBoxAdapter : IMultiSelector
    {
        private readonly ListBox listBox;

        public ListBoxAdapter(ListBox listBox)
        {
            this.listBox = listBox;
        }

        public Selector Selector => listBox;

        public IList SelectedItems => listBox.SelectedItems;
    }

    private class MultiSelectorAdapter : IMultiSelector
    {
        private readonly MultiSelector multiSelector;

        public MultiSelectorAdapter(MultiSelector multiSelector)
        {
            this.multiSelector = multiSelector;
        }

        public Selector Selector => multiSelector;

        public IList SelectedItems => multiSelector.SelectedItems;
    }
}
