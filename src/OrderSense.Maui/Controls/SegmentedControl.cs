using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace OrderSense.Maui.Controls;

[ContentProperty(nameof(Items))]
public class SegmentedControl : ContentView
{
    public static readonly BindableProperty SelectedValueProperty = BindableProperty.Create(
        nameof(SelectedValue), typeof(object), typeof(SegmentedControl),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, _, _) => ((SegmentedControl)bindable).UpdateSelection());

    public static readonly BindableProperty StretchProperty = BindableProperty.Create(
        nameof(Stretch), typeof(bool), typeof(SegmentedControl), false,
        propertyChanged: (bindable, _, _) => ((SegmentedControl)bindable).Rebuild());

    private readonly Border _track;
    private readonly Grid _row = new() { ColumnSpacing = 0 };
    private readonly List<(SegmentItem Item, Border Frame, Label? Icon, Label Text)> _segments = [];

    public SegmentedControl()
    {
        var items = new ObservableCollection<SegmentItem>();
        items.CollectionChanged += OnItemsChanged;
        Items = items;

        _track = new Border { Content = _row };
        _track.SetDynamicResource(StyleProperty, "SegmentTrack");
        Content = _track;
    }

    public IList<SegmentItem> Items { get; }

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public bool Stretch
    {
        get => (bool)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        _row.Children.Clear();
        _row.ColumnDefinitions.Clear();
        _segments.Clear();
        _track.HorizontalOptions = Stretch ? LayoutOptions.Fill : LayoutOptions.Start;

        foreach (var item in Items)
        {
            Label? icon = null;
            var text = new Label { Text = item.Text };
            var content = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center };

            if (!string.IsNullOrEmpty(item.Icon))
            {
                icon = new Label { Text = item.Icon };
                content.Add(icon);
            }

            content.Add(text);

            var frame = new Border { Content = content };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => SelectedValue = item.Value;
            frame.GestureRecognizers.Add(tap);

            _row.ColumnDefinitions.Add(new ColumnDefinition(Stretch ? GridLength.Star : GridLength.Auto));
            _row.Add(frame, _row.ColumnDefinitions.Count - 1);
            _segments.Add((item, frame, icon, text));
        }

        UpdateSelection();
    }

    private void UpdateSelection()
    {
        foreach (var (item, frame, icon, text) in _segments)
        {
            var selected = Equals(item.Value?.ToString(), SelectedValue?.ToString());

            frame.SetDynamicResource(StyleProperty, selected ? "SegmentSelected" : "Segment");
            text.SetDynamicResource(StyleProperty, selected ? "SegmentTextSelected" : "SegmentText");
            icon?.SetDynamicResource(StyleProperty, selected ? "SegmentIconSelected" : "SegmentIcon");

            SemanticProperties.SetDescription(frame, selected ? item.Text + ", вибрано" : item.Text);
        }
    }
}

public class SegmentItem
{
    public string Text { get; set; } = "";

    public string? Icon { get; set; }

    public object? Value { get; set; }
}
