using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Novolis.IO.Ndjson;
using Novolis.Maui.GraphicalProfile;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.Ndjson;

/// <summary>
/// Bounded, virtualized MAUI presentation for one <see cref="INdjsonDocument"/>.
/// The host remains responsible for selecting and materializing files.
/// </summary>
public sealed class NdjsonSliceView : ContentView
{
    private readonly Label _documentMeta;
    private readonly Label _rangeLabel;
    private readonly Label _sliceSummary;
    private readonly Label _emptyLabel;
    private readonly Label _filterStatus;
    private readonly SearchBar _filterBar;
    private readonly Button _refreshButton;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _jumpButton;
    private readonly Entry _jumpEntry;
    private readonly Picker _takePicker;
    private readonly CollectionView _records;
    private readonly Border _card;
    private ViewerState _state = new(0, 100, null, false, null);
    private INdjsonDocument? _document;
    private IReadOnlyList<NdjsonRecordDisplay> _sliceRecords = [];
    private string _displayName = "NDJSON document";
    private bool _themeSubscribed;

    /// <summary>Creates the record-slice view.</summary>
    public NdjsonSliceView()
    {
        AutomationId = "NdjsonSliceView";
        _documentMeta = new Label
        {
            AutomationId = "NdjsonDocumentMeta",
            Text = "No document open",
            FontFamily = Profile.FontFamily,
            FontSize = 12,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
        };
        _rangeLabel = new Label
        {
            AutomationId = "NdjsonRange",
            Text = "No records",
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
        };
        _sliceSummary = new Label
        {
            AutomationId = "NdjsonSliceSummary",
            Text = "Open an NDJSON file to inspect its records.",
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 12,
        };
        _emptyLabel = new Label
        {
            AutomationId = "NdjsonEmpty",
            Text = "Open an NDJSON file to inspect its records.",
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(18),
        };

        _refreshButton = CreateButton("Refresh", "NdjsonRefresh", RefreshAsync);
        _previousButton = CreateButton("Previous", "NdjsonPrevious", PreviousAsync);
        _nextButton = CreateButton("Next", "NdjsonNext", NextAsync);
        _jumpButton = CreateButton("Go", "NdjsonJump", JumpAsync);
        _jumpEntry = new Entry
        {
            AutomationId = "NdjsonJumpEntry",
            Placeholder = "Record number",
            Keyboard = Keyboard.Numeric,
            HorizontalOptions = LayoutOptions.Fill,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
        };
        _takePicker = new Picker
        {
            AutomationId = "NdjsonTakePicker",
            Title = "Records per slice",
            ItemsSource = new[] { "50", "100", "250", "500", "1000" },
            SelectedItem = "100",
            HorizontalOptions = LayoutOptions.Fill,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
        };
        _takePicker.SelectedIndexChanged += async (_, _) => await ChangeTakeAsync();
        _filterBar = new SearchBar
        {
            AutomationId = "NdjsonFilter",
            Placeholder = "Filter loaded records",
            HorizontalOptions = LayoutOptions.Fill,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
        };
        _filterBar.TextChanged += (_, _) => RenderFilteredRecords();
        _filterStatus = new Label
        {
            AutomationId = "NdjsonFilterStatus",
            FontSize = 12,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.End,
        };

        _records = new CollectionView
        {
            AutomationId = "NdjsonRecords",
            SelectionMode = SelectionMode.None,
            ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
            {
                ItemSpacing = 10,
            },
            EmptyView = _emptyLabel,
            ItemTemplate = new DataTemplate(() =>
            {
                var card = new NdjsonRecordCard
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    Margin = new Thickness(12, 0),
                };
                card.SetBinding(NdjsonRecordCard.RecordProperty, ".");
                return card;
            }),
        };

        var heading = new Grid
        {
            Padding = new Thickness(16, 14, 16, 0),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
        };
        heading.Add(new Label
        {
            Text = "RECORDS",
            FontAttributes = FontAttributes.Bold,
            FontSize = 10,
            CharacterSpacing = 1.4,
        }, 0, 0);
        heading.Add(_documentMeta, 0, 1);

        var filterRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        filterRow.Add(_filterBar, 0, 0);
        filterRow.Add(_refreshButton, 1, 0);

        var navigation = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.4, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
            },
            ColumnSpacing = 8,
        };
        navigation.Add(_previousButton, 0, 0);
        navigation.Add(new VerticalStackLayout
        {
            Spacing = 2,
            HorizontalOptions = LayoutOptions.Fill,
            Children = { _rangeLabel, _sliceSummary },
        }, 1, 0);
        navigation.Add(_nextButton, 2, 0);

        var jumpRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        jumpRow.Add(_jumpEntry, 0, 0);
        jumpRow.Add(_jumpButton, 1, 0);

        var pageSizeRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = 8,
        };
        pageSizeRow.Add(new Label
        {
            Text = "Records per slice",
            VerticalTextAlignment = TextAlignment.Center,
            FontSize = 12,
        }, 0, 0);
        pageSizeRow.Add(_takePicker, 1, 0);
        pageSizeRow.Add(_filterStatus, 2, 0);

        var controls = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(12, 12, 12, 10),
            Children =
            {
                filterRow,
                navigation,
                jumpRow,
                pageSizeRow,
            },
        };
        var viewerLayout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
            RowSpacing = 0,
        };
        viewerLayout.Add(heading, 0, 0);
        viewerLayout.Add(controls, 0, 1);
        viewerLayout.Add(_records, 0, 2);
        _card = CreateCard(viewerLayout);
        _card.Padding = new Thickness(0, 0, 0, 10);
        Content = _card;

        _previousButton.IsEnabled = false;
        _nextButton.IsEnabled = false;
        ApplyTheme();
    }

    /// <summary>The currently displayed document.</summary>
    public INdjsonDocument? Document => _document;

    /// <summary>
    /// Optional host refresh operation. It can rematerialize an Android content
    /// URI and return the replacement document.
    /// </summary>
    public Func<CancellationToken, Task<INdjsonDocument?>>? RefreshDocumentAsync { get; set; }

    /// <summary>Optional host error presentation callback.</summary>
    public Func<string, Exception, Task>? ErrorHandler { get; set; }

    /// <summary>Opens a document and loads the current slice.</summary>
    public async Task OpenAsync(
        INdjsonDocument document,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        _displayName = string.IsNullOrWhiteSpace(displayName)
            ? document.File.Name
            : displayName;
        _filterBar.Text = string.Empty;
        _state = _state with { Skip = 0, Slice = null, Error = null };
        _documentMeta.Text = _displayName;
        SetBusy(true);
        try
        {
            await LoadSliceAsync(cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _state = _state with { Error = error };
            await ReportErrorAsync("Unable to read NDJSON", error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Applies the current graphical-profile colors.</summary>
    public void ApplyTheme()
    {
        BackgroundColor = Profile.Background;
        _documentMeta.TextColor = Profile.Muted;
        _rangeLabel.TextColor = Profile.Text;
        _sliceSummary.TextColor = Profile.Muted;
        _emptyLabel.TextColor = Profile.Muted;
        _filterStatus.TextColor = Profile.Muted;
        _card.Background = new SolidColorBrush(Profile.Surface);
        _card.Stroke = new SolidColorBrush(Profile.Border);
        _refreshButton.BackgroundColor = Profile.ActionSoft;
        _refreshButton.TextColor = Profile.Text;
        _previousButton.BackgroundColor = Profile.Raised;
        _previousButton.TextColor = Profile.Text;
        _nextButton.BackgroundColor = Profile.Raised;
        _nextButton.TextColor = Profile.Text;
        _jumpButton.BackgroundColor = Profile.Action;
        _jumpButton.TextColor = Profile.OnAction;
        _jumpEntry.TextColor = Profile.Text;
        _jumpEntry.BackgroundColor = Profile.Raised;
        _takePicker.TextColor = Profile.Text;
        _takePicker.BackgroundColor = Profile.Raised;
        _filterBar.TextColor = Profile.Text;
        _filterBar.BackgroundColor = Profile.Raised;
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (!_themeSubscribed && Application.Current is { } application)
        {
            application.RequestedThemeChanged += OnRequestedThemeChanged;
            _themeSubscribed = true;
        }
    }

    private async Task RefreshAsync()
    {
        if (_document is null)
            return;

        _state = ViewerNavigation.BeginRefresh(_state);
        SetBusy(true);
        try
        {
            if (RefreshDocumentAsync is { } refresh)
                _document = await refresh(CancellationToken.None) ?? _document;
            else
                await _document.RefreshAsync();

            await LoadSliceAsync(CancellationToken.None);
        }
        catch (Exception error)
        {
            _state = _state with { Error = error };
            await ReportErrorAsync("Unable to refresh NDJSON", error);
        }
        finally
        {
            _state = ViewerNavigation.CompleteRefresh(_state);
            SetBusy(false);
        }
    }

    private async Task PreviousAsync()
    {
        if (_document is null)
            return;

        _state = ViewerNavigation.Previous(_state);
        await TryLoadSliceAsync();
    }

    private async Task NextAsync()
    {
        if (_document is null)
            return;

        _state = ViewerNavigation.Next(_state);
        await TryLoadSliceAsync();
    }

    private async Task JumpAsync()
    {
        if (!long.TryParse(_jumpEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var skip)
            || skip < 0)
        {
            await ReportErrorAsync(
                "Jump to record",
                new ArgumentException("Enter a non-negative record number."));
            return;
        }

        _state = ViewerNavigation.Jump(_state, skip);
        await TryLoadSliceAsync();
    }

    private async Task ChangeTakeAsync()
    {
        if (!int.TryParse(_takePicker.SelectedItem as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var take))
            return;

        _state = ViewerNavigation.ChangeTake(_state, take);
        await TryLoadSliceAsync();
    }

    private async Task LoadSliceAsync(CancellationToken cancellationToken)
    {
        var document = _document;
        if (document is null)
            return;

        var slice = await document.ReadAsync(
            _state.Skip,
            _state.Take,
            cancellationToken);
        _state = _state with
        {
            Slice = slice,
            IsRefreshing = false,
            Error = null,
        };
        RenderSlice(document);
    }

    private async Task TryLoadSliceAsync()
    {
        try
        {
            SetBusy(true);
            await LoadSliceAsync(CancellationToken.None);
        }
        catch (Exception error)
        {
            _state = _state with { Error = error };
            await ReportErrorAsync("Unable to read NDJSON", error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RenderSlice(INdjsonDocument document)
    {
        var slice = _state.Slice;
        _sliceRecords = slice?.Records.Select(static record => new NdjsonRecordDisplay(record)).ToArray() ?? [];
        if (slice is null || slice.Records.Count == 0)
        {
            _emptyLabel.Text = "This slice has no records.";
            _rangeLabel.Text = "No records";
            _sliceSummary.Text = "Try another slice or refresh the file.";
        }
        else
        {
            var first = slice.Records[0];
            var last = slice.Records[^1];
            _emptyLabel.Text = "No records match this filter.";
            _rangeLabel.Text = $"{first.Number:N0} – {last.Number:N0}";
            _sliceSummary.Text = $"Showing {slice.Records.Count:N0} loaded records";
        }

        document.File.Refresh();
        var recordSuffix = slice?.HasMore is true ? "+" : string.Empty;
        _documentMeta.Text = $"{_displayName} · {FormatBytes(document.File.Length)} · {document.RecordCount:N0}{recordSuffix} records";
        RenderFilteredRecords();
        _previousButton.IsEnabled = slice?.HasPrevious is true;
        _nextButton.IsEnabled = slice?.HasMore is true;
        ApplyTheme();
    }

    private void RenderFilteredRecords()
    {
        var query = _filterBar.Text?.Trim();
        var visible = string.IsNullOrWhiteSpace(query)
            ? _sliceRecords
            : _sliceRecords
                .Where(record => record.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        _records.ItemsSource = visible;
        _filterStatus.Text = string.IsNullOrWhiteSpace(query)
            ? $"{_sliceRecords.Count:N0} loaded"
            : $"{visible.Count:N0} match{(visible.Count == 1 ? string.Empty : "es")}";
        _emptyLabel.Text = _sliceRecords.Count == 0
            ? "This slice has no records."
            : "No records match this filter.";
    }

    private async Task ReportErrorAsync(string title, Exception error)
    {
        if (ErrorHandler is { } handler)
            await handler(title, error);
    }

    private void SetBusy(bool isBusy)
    {
        _refreshButton.IsEnabled = !isBusy && _document is not null;
        _previousButton.IsEnabled = !isBusy && _state.Slice?.HasPrevious is true;
        _nextButton.IsEnabled = !isBusy && _state.Slice?.HasMore is true;
        _jumpButton.IsEnabled = !isBusy;
        _jumpEntry.IsEnabled = !isBusy;
        _takePicker.IsEnabled = !isBusy;
        _filterBar.IsEnabled = !isBusy;
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args) =>
        ApplyTheme();

    private static string FormatBytes(long bytes)
    {
        var value = (double)Math.Max(0, bytes);
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static Button CreateButton(
        string text,
        string automationId,
        Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            AutomationId = automationId,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            FontSize = GraphicalProfileColors.ButtonSize,
            BackgroundColor = Profile.Raised,
            TextColor = Profile.Text,
            BorderColor = Profile.Border,
            BorderWidth = GraphicalProfileColors.Stroke,
            CornerRadius = (int)GraphicalProfileColors.PrimaryRadius,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
            Padding = new Thickness(16, 9),
        };
        SemanticProperties.SetDescription(button, text);
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private static Border CreateCard(View content) =>
        new()
        {
            Padding = new Thickness(GraphicalProfileColors.CardPadding),
            StrokeThickness = GraphicalProfileColors.Stroke,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(GraphicalProfileColors.CardRadius) },
            Content = content,
        };
}
