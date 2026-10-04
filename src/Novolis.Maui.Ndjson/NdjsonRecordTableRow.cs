using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using Novolis.Maui.GraphicalProfile;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.Ndjson;

/// <summary>Virtualized, horizontally scrollable table row for one NDJSON record.</summary>
public sealed class NdjsonRecordTableRow : ContentView
{
    private readonly Grid _grid = new() { ColumnSpacing = 0, RowSpacing = 0 };
    private readonly Label _details = new()
    {
        FontFamily = Profile.MonoFontFamily,
        FontSize = 12,
        LineBreakMode = LineBreakMode.NoWrap,
        Padding = new Thickness(12, 10),
    };
    private readonly ScrollView _detailsScroller;
    private readonly Border _rowBorder;
    private bool _expanded;

    /// <summary>Identifies the table row binding.</summary>
    public static readonly BindableProperty RowProperty =
        BindableProperty.Create(
            nameof(Row),
            typeof(NdjsonTableRow),
            typeof(NdjsonRecordTableRow),
            default(NdjsonTableRow),
            propertyChanged: static (bindable, _, _) =>
                ((NdjsonRecordTableRow)bindable).RenderRow());

    /// <summary>Creates a table row.</summary>
    public NdjsonRecordTableRow()
    {
        AutomationId = "NdjsonRecordRow";
        _detailsScroller = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            HeightRequest = 220,
            IsVisible = false,
            Content = new Border
            {
                Background = new SolidColorBrush(Profile.Background),
                Stroke = new SolidColorBrush(Profile.Border),
                StrokeThickness = GraphicalProfileColors.Stroke,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
                Content = _details,
            },
        };
        _rowBorder = new Border
        {
            StrokeThickness = GraphicalProfileColors.Stroke,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(0) },
            Content = _grid,
        };
        Content = _rowBorder;
        ApplyTheme();
    }

    /// <summary>Gets or sets the row model.</summary>
    public NdjsonTableRow? Row
    {
        get => (NdjsonTableRow?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    /// <summary>Applies the current graphical-profile colors.</summary>
    public void ApplyTheme()
    {
        _rowBorder.Background = new SolidColorBrush(Profile.Raised);
        _rowBorder.Stroke = new SolidColorBrush(Profile.Border);
        _details.TextColor = Profile.Text;
        if (Row is { } row)
            RenderRow(row, preserveExpanded: _expanded);
    }

    private void RenderRow()
    {
        _expanded = false;
        RenderRow(Row, preserveExpanded: false);
    }

    private void RenderRow(NdjsonTableRow? row, bool preserveExpanded)
    {
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();
        if (row is null)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
        var columns = row.Columns;
        _grid.RowDefinitions.Add(new RowDefinition(new GridLength(62)));
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddColumn(NdjsonTableColumn.RecordWidth);
        AddColumn(NdjsonTableColumn.StatusWidth);
        foreach (var column in columns)
            AddColumn(column.Width);
        AddColumn(NdjsonTableColumn.ActionsWidth);

        var columnIndex = 0;
        AddCell(CreateLabel($"#{row.Record.Number:N0}", bold: true), columnIndex++);
        AddCell(CreateLabel(row.Record.Status, bold: true, color: row.Record.IsValid ? Profile.Accent : Profile.Danger), columnIndex++);
        foreach (var column in columns)
        {
            var value = row.ValueFor(column.Name);
            AddCell(CreateLabel(value, color: value == "—" ? Profile.Muted : Profile.Text), columnIndex++);
        }

        var expand = CreateButton(preserveExpanded ? "Collapse" : "Expand", $"NdjsonExpand{row.Record.Number}");
        expand.Clicked += OnExpandClicked;
        var copy = CreateButton("Copy", $"NdjsonCopy{row.Record.Number}");
        copy.Clicked += OnCopyClicked;
        var actions = new HorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(6, 0),
            VerticalOptions = LayoutOptions.Center,
            Children = { expand, copy },
        };
        AddCell(actions, columnIndex);

        Grid.SetColumn(_detailsScroller, 0);
        Grid.SetColumnSpan(_detailsScroller, columnIndex + 1);
        Grid.SetRow(_detailsScroller, 1);
        _grid.Add(_detailsScroller);
        _detailsScroller.IsVisible = preserveExpanded;
        if (preserveExpanded)
            _details.Text = row.Record.Details;
        SemanticProperties.SetDescription(this, $"NDJSON table row {row.Record.Number}");
    }

    private void AddColumn(double width) =>
        _grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(width)));

    private void AddCell(View content, int column)
    {
        var cell = new Border
        {
            Background = new SolidColorBrush(Profile.Raised),
            Stroke = new SolidColorBrush(Profile.Border),
            StrokeThickness = GraphicalProfileColors.Stroke,
            Content = content,
        };
        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, 0);
        _grid.Add(cell);
    }

    private static Label CreateLabel(string text, bool bold = false, Color? color = null) =>
        new()
        {
            Text = text,
            TextColor = color ?? Profile.Text,
            FontFamily = Profile.MonoFontFamily,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            FontSize = 12,
            MaxLines = 2,
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(10, 8),
        };

    private static Button CreateButton(string text, string automationId) =>
        new()
        {
            Text = text,
            AutomationId = automationId,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            FontSize = 11,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
            Padding = new Thickness(9, 5),
            CornerRadius = (int)GraphicalProfileColors.ControlRadius,
            BorderWidth = GraphicalProfileColors.Stroke,
            BorderColor = Profile.Border,
            BackgroundColor = Profile.ActionSoft,
            TextColor = Profile.Text,
        };

    private void OnExpandClicked(object? sender, EventArgs args)
    {
        if (Row is not { } row)
            return;
        _expanded = !_expanded;
        _details.Text = _expanded ? row.Record.Details : string.Empty;
        _detailsScroller.IsVisible = _expanded;
        if (sender is Button button)
            button.Text = _expanded ? "Collapse" : "Expand";
    }

    private async void OnCopyClicked(object? sender, EventArgs args)
    {
        if (Row is not { } row)
            return;
        await Clipboard.Default.SetTextAsync(row.Record.CopyText);
    }
}
