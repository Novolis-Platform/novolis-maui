using System.Globalization;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using Novolis.IO.Ndjson;
using Novolis.Maui.GraphicalProfile;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.Ndjson;

/// <summary>
/// Bounded MAUI presentation for one <see cref="INdjsonDocument"/>.
/// The host remains responsible for selecting and materializing files.
/// </summary>
public sealed class NdjsonSliceView : ContentView
{
    private readonly Label _documentMeta;
    private readonly Label _rangeLabel;
    private readonly Label _emptyLabel;
    private readonly Button _refreshButton;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _jumpButton;
    private readonly Entry _jumpEntry;
    private readonly Picker _takePicker;
    private readonly VerticalStackLayout _records;
    private readonly Border _card;
    private NdjsonSliceState _state = new(0, 100, null, false, null);
    private INdjsonDocument? _document;
    private bool _themeSubscribed;

    /// <summary>Creates the record-slice view.</summary>
    public NdjsonSliceView()
    {
        _documentMeta = new Label
        {
            AutomationId = "NdjsonDocumentMeta",
            Text = "No document open",
            FontFamily = Profile.FontFamily,
            FontSize = 12,
        };
        _rangeLabel = new Label
        {
            AutomationId = "NdjsonRange",
            Text = "No records",
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };
        _emptyLabel = new Label
        {
            Text = "Open an NDJSON file to inspect its records.",
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };

        _refreshButton = CreateButton("Refresh", "NdjsonRefresh", RefreshAsync);
        _previousButton = CreateButton("Previous", "NdjsonPrevious", PreviousAsync);
        _nextButton = CreateButton("Next", "NdjsonNext", NextAsync);
        _jumpButton = CreateButton("Jump", "NdjsonJump", JumpAsync);
        _jumpEntry = new Entry
        {
            AutomationId = "NdjsonJumpEntry",
            Placeholder = "Record number",
            Keyboard = Keyboard.Numeric,
            HorizontalOptions = LayoutOptions.Fill,
        };
        _takePicker = new Picker
        {
            AutomationId = "NdjsonTakePicker",
            Title = "Records per slice",
            ItemsSource = new[] { "50", "100", "250", "500", "1000" },
            SelectedItem = "100",
            HorizontalOptions = LayoutOptions.Fill,
        };
        _takePicker.SelectedIndexChanged += async (_, _) => await ChangeTakeAsync();

        _records = new VerticalStackLayout
        {
            AutomationId = "NdjsonRecords",
            Spacing = 8,
            Padding = new Thickness(12, 4, 12, 16),
        };

        var navigation = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        navigation.Add(_previousButton, 0, 0);
        navigation.Add(_rangeLabel, 1, 0);
        navigation.Add(_nextButton, 2, 0);

        var controls = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        controls.Add(new Label
        {
            Text = "Records per slice",
            VerticalTextAlignment = TextAlignment.Center,
        }, 0, 0);
        controls.Add(_takePicker, 1, 0);
        controls.Add(_jumpEntry, 2, 0);
        controls.Add(_jumpButton, 3, 0);
        controls.Add(_refreshButton, 4, 0);

        var viewerLayout = new Grid
        {
            Padding = new Thickness(12, 12, 12, 0),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
            RowSpacing = 10,
        };
        viewerLayout.Add(new Label
        {
            Text = "RECORDS",
            FontAttributes = FontAttributes.Bold,
            FontSize = 10,
            CharacterSpacing = 1.4,
        }, 0, 0);
        viewerLayout.Add(_documentMeta, 0, 1);
        viewerLayout.Add(navigation, 0, 2);
        viewerLayout.Add(controls, 0, 3);
        viewerLayout.Add(new ScrollView
        {
            AutomationId = "NdjsonRecordScroll",
            Content = _records,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
        }, 0, 4);
        viewerLayout.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        _card = CreateCard(viewerLayout);
        _card.Padding = new Thickness(0);
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
        _state = _state with { Skip = 0, Slice = null, Error = null };
        _documentMeta.Text = displayName is null
            ? "Loading..."
            : displayName;
        SetBusy(true);
        try
        {
            await LoadSliceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _state = _state with { Error = error };
            await ReportErrorAsync("Unable to read NDJSON", error).ConfigureAwait(false);
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
        _emptyLabel.TextColor = Profile.Muted;
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
    }

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

        _state = _state with { IsRefreshing = true, Error = null };
        SetBusy(true);
        try
        {
            if (RefreshDocumentAsync is { } refresh)
                _document = await refresh(CancellationToken.None).ConfigureAwait(false) ?? _document;
            else
                await _document.RefreshAsync().ConfigureAwait(false);

            await LoadSliceAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            _state = _state with { Error = error };
            await ReportErrorAsync("Unable to refresh NDJSON", error).ConfigureAwait(false);
        }
        finally
        {
            _state = _state with { IsRefreshing = false };
            SetBusy(false);
        }
    }

    private async Task PreviousAsync()
    {
        if (_document is null)
            return;

        _state = NdjsonSliceNavigation.Previous(_state);
        await TryLoadSliceAsync().ConfigureAwait(false);
    }

    private async Task NextAsync()
    {
        if (_document is null)
            return;

        _state = NdjsonSliceNavigation.Next(_state);
        await TryLoadSliceAsync().ConfigureAwait(false);
    }

    private async Task JumpAsync()
    {
        if (!long.TryParse(_jumpEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var skip)
            || skip < 0)
        {
            await ReportErrorAsync(
                "Jump to record",
                new ArgumentException("Enter a non-negative record number.")).ConfigureAwait(false);
            return;
        }

        _state = NdjsonSliceNavigation.Jump(_state, skip);
        await TryLoadSliceAsync().ConfigureAwait(false);
    }

    private async Task ChangeTakeAsync()
    {
        if (!int.TryParse(_takePicker.SelectedItem as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var take))
            return;

        _state = NdjsonSliceNavigation.ChangeTake(_state, take);
        await TryLoadSliceAsync().ConfigureAwait(false);
    }

    private async Task LoadSliceAsync(CancellationToken cancellationToken)
    {
        var document = _document;
        if (document is null)
            return;

        var slice = await document.ReadAsync(
            _state.Skip,
            _state.Take,
            cancellationToken).ConfigureAwait(false);
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
            await LoadSliceAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            _state = _state with { Error = error };
            await ReportErrorAsync("Unable to read NDJSON", error).ConfigureAwait(false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RenderSlice(INdjsonDocument document)
    {
        var slice = _state.Slice;
        _records.Children.Clear();
        if (slice is null || slice.Records.Count == 0)
        {
            _emptyLabel.IsVisible = true;
            _records.Children.Add(_emptyLabel);
        }
        else
        {
            _emptyLabel.IsVisible = false;
            foreach (var record in slice.Records)
                _records.Children.Add(CreateRecordView(new NdjsonRecordDisplay(record)));
        }

        var first = slice?.Records.FirstOrDefault();
        var last = slice?.Records.LastOrDefault();
        _rangeLabel.Text = first is null || last is null
            ? $"{_state.Skip:N0} · no records"
            : $"{first.Number:N0} – {last.Number:N0}";
        document.File.Refresh();
        _documentMeta.Text = $"{FormatBytes(document.File.Length)} · {document.RecordCount:N0}"
            + (slice?.HasMore is true ? "+" : string.Empty)
            + " complete records";
        _previousButton.IsEnabled = slice?.HasPrevious is true;
        _nextButton.IsEnabled = slice?.HasMore is true;
        ApplyTheme();
    }

    private View CreateRecordView(NdjsonRecordDisplay record)
    {
        var details = new Editor
        {
            Text = record.Details,
            IsReadOnly = true,
            IsVisible = false,
            FontFamily = "Consolas",
            FontSize = 12,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MaximumHeightRequest = 280,
        };
        Button? expand = null;
        expand = CreateButton("Expand", $"NdjsonExpand{record.Number}", () =>
        {
            details.IsVisible = !details.IsVisible;
            expand!.Text = details.IsVisible ? "Collapse" : "Expand";
            return Task.CompletedTask;
        });
        var copy = CreateButton("Copy", $"NdjsonCopy{record.Number}", async () =>
        {
            await Clipboard.Default.SetTextAsync(record.CopyText).ConfigureAwait(false);
        });
        expand.Padding = new Thickness(10, 5);
        copy.Padding = new Thickness(10, 5);
        var actions = new HorizontalStackLayout
        {
            Spacing = 6,
            Children = { expand, copy },
        };
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        header.Add(new Label
        {
            Text = record.Number.ToString("N0", CultureInfo.InvariantCulture),
            FontFamily = "Consolas",
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center,
        }, 0, 0);
        header.Add(new Label
        {
            Text = record.Status,
            FontAttributes = FontAttributes.Bold,
            FontSize = 11,
            TextColor = Profile.Accent,
            VerticalTextAlignment = TextAlignment.Center,
        }, 1, 0);
        header.Add(actions, 2, 0);

        var content = new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                header,
                new Label
                {
                    Text = record.Preview,
                    FontFamily = "Consolas",
                    FontSize = 12,
                    LineBreakMode = LineBreakMode.WordWrap,
                },
                details,
            },
        };
        var border = CreateCard(content);
        border.Padding = new Thickness(12, 10);
        return border;
    }

    private async Task ReportErrorAsync(string title, Exception error)
    {
        if (ErrorHandler is { } handler)
            await handler(title, error).ConfigureAwait(false);
    }

    private void SetBusy(bool isBusy)
    {
        _refreshButton.IsEnabled = !isBusy && _document is not null;
        _previousButton.IsEnabled = !isBusy && _state.Slice?.HasPrevious is true;
        _nextButton.IsEnabled = !isBusy && _state.Slice?.HasMore is true;
        _jumpButton.IsEnabled = !isBusy;
        _takePicker.IsEnabled = !isBusy;
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
        button.Clicked += async (_, _) => await action().ConfigureAwait(false);
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

    private sealed record NdjsonSliceState(
        long Skip,
        int Take,
        NdjsonSlice? Slice,
        bool IsRefreshing,
        Exception? Error);

    private static class NdjsonSliceNavigation
    {
        public static NdjsonSliceState Previous(NdjsonSliceState state) =>
            state with { Skip = Math.Max(0, state.Skip - state.Take), Error = null };

        public static NdjsonSliceState Next(NdjsonSliceState state) =>
            state.Slice is not { HasMore: true }
                ? state
                : state with { Skip = checked(state.Skip + state.Take), Error = null };

        public static NdjsonSliceState Jump(NdjsonSliceState state, long skip) =>
            state with { Skip = skip, Error = null };

        public static NdjsonSliceState ChangeTake(NdjsonSliceState state, int take) =>
            state with { Take = take, Error = null };
    }
}
