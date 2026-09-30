using System.Security.Cryptography;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Documents;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Platform;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using Novolis.Pdf.Text;

namespace Novolis.Maui.PdfViewer;

/// <summary>Reusable local PDF page viewer for MAUI Windows and Android hosts.</summary>
public sealed class PdfViewer : ContentView
{
    private const double DefaultDpi = 120;
    private const double ThumbnailDpi = 56;
    private const double RailWidth = 156;
    private readonly PdfLimits _limits;
    private readonly IPdfDocumentStateStore? _stateStore;
    private readonly PdfSkiaPageRenderer _renderer;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _zoomOutButton;
    private readonly Button _zoomInButton;
    private readonly Button _fitButton;
    private readonly Button _rotateButton;
    private readonly Label _pageLabel;
    private readonly Entry _pageEntry;
    private readonly SearchBar _searchBar;
    private readonly Label _searchStatus;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly CollectionView _pageRail;
    private readonly PdfPageView _readingPage;
    private readonly ScrollView _readingScroll;
    private readonly Grid _readingHost;
    private readonly Grid _workspace;
    private readonly PdfTextExtractor _textExtractor;
    private readonly PdfTextSearch _textSearch = new();
    private PdfDocumentSession? _session;
    private IReadOnlyList<PdfResolvedPage> _pages = [];
    private IReadOnlyList<PdfTextSpan> _textSpans = [];
    private int _pageIndex;
    private double _zoom = 1;
    private int _rotation;
    private bool _isBusy;
    private bool _railSync;
    private double _paneWidth = 720;
    private double _paneHeight = 720;
    private double _pinchStart = 1;

    /// <summary>Creates a viewer with optional local reading-position persistence.</summary>
    public PdfViewer(
        IPdfDocumentStateStore? stateStore = null,
        PdfLimits? limits = null)
    {
        _limits = limits ?? PdfLimits.Default;
        _limits.Validate();
        _stateStore = stateStore;
        _renderer = new PdfSkiaPageRenderer(_limits);
        _textExtractor = new PdfTextExtractor(_limits);

        _previousButton = CreateToolbarButton("Previous", "PdfPreviousPage", GoPreviousAsync);
        _nextButton = CreateToolbarButton("Next", "PdfNextPage", GoNextAsync);
        _zoomOutButton = CreateToolbarButton("Zoom −", "PdfZoomOut", ZoomOutAsync);
        _zoomInButton = CreateToolbarButton("Zoom +", "PdfZoomIn", ZoomInAsync);
        _fitButton = CreateToolbarButton("Fit", "PdfFit", FitAsync);
        _rotateButton = CreateToolbarButton("Rotate", "PdfRotate", () => RotateAsync());
        _pageLabel = CreateLabel("0 / 0", "PdfPageLabel");
        _pageEntry = new Entry
        {
            AutomationId = "PdfPageEntry",
            Placeholder = "Page",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 64,
            HorizontalTextAlignment = TextAlignment.Center,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
        };
        _pageEntry.Completed += (_, _) => GoToEnteredPageAsync();
        _searchBar = new SearchBar
        {
            AutomationId = "PdfSearch",
            Placeholder = "Find in document",
            WidthRequest = 220,
            HorizontalOptions = LayoutOptions.Fill,
        };
        _searchBar.SearchButtonPressed += (_, _) => SearchAsync();
        _searchStatus = CreateLabel(string.Empty, "PdfSearchStatus");
        _titleLabel = CreateLabel("No document open", "PdfDocumentTitle");
        _statusLabel = CreateLabel("Choose a local PDF to begin.", "PdfStatus");
        _pageRail = new CollectionView
        {
            AutomationId = "PdfPageRail",
            SelectionMode = SelectionMode.Single,
            WidthRequest = RailWidth,
            BackgroundColor = Profile.Surface,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
            {
                ItemSpacing = 8,
            },
        };
        _pageRail.ItemTemplate = new DataTemplate(() =>
        {
            var page = new PdfPageView
            {
                AutomationId = "PdfPageThumb",
                RenderPageAsync = RenderThumbnailAsync,
                RenderFailed = OnPageRenderFailed,
                PageWidth = 128,
            };
            page.SetBinding(PdfPageView.PageIndexProperty, ".");
            return page;
        });
        _pageRail.SelectionChanged += OnRailSelectionChanged;
        _readingPage = new PdfPageView
        {
            AutomationId = "PdfReadingPage",
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            RenderPageAsync = RenderReadingAsync,
            RenderFailed = OnPageRenderFailed,
        };
        _readingPage.SetBinding(
            PdfPageView.PageWidthProperty,
            new Binding(nameof(ReadingPageWidth), source: this));
        _readingScroll = new ScrollView
        {
            AutomationId = "PdfReadingScroll",
            Orientation = ScrollOrientation.Both,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
            Content = _readingPage,
        };
        _readingHost = new Grid { BackgroundColor = Profile.Background };
        _readingHost.Add(_readingScroll);
        _workspace = new Grid
        {
            ColumnSpacing = 8,
            Padding = new Thickness(8, 0, 8, 8),
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(RailWidth)),
                new ColumnDefinition(GridLength.Star),
            },
        };
        _workspace.Add(_pageRail, 0, 0);
        _workspace.Add(_readingHost, 1, 0);
        SizeChanged += (_, _) => ApplyWorkspaceColumns();

        var toolbarItems = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(12, 10),
            Children =
            {
                _previousButton,
                _nextButton,
                _zoomOutButton,
                _zoomInButton,
                _fitButton,
                _rotateButton,
                _pageLabel,
                _pageEntry,
                _searchBar,
                _searchStatus,
            },
        };
        var toolbar = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = toolbarItems,
        };

        var header = new Grid
        {
            Padding = new Thickness(16, 14, 16, 8),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
        };
        header.Add(_titleLabel, 0, 0);
        header.Add(_statusLabel, 0, 1);

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
            BackgroundColor = Profile.Background,
        };
        layout.Add(header, 0, 0);
        layout.Add(toolbar, 0, 1);
        layout.Add(_workspace, 0, 2);
        Content = layout;

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        _readingScroll.GestureRecognizers.Add(pinch);
        ApplyWorkspaceColumns();
        ApplyTheme();
        UpdateToolbar();
    }

    /// <summary>Raised after the current page changes.</summary>
    public event EventHandler? PageChanged;

    /// <summary>Raised when opening or rendering fails.</summary>
    public event EventHandler<PdfViewerErrorEventArgs>? Error;

    /// <summary>Current reader session, if a document is open.</summary>
    public PdfDocumentSession? Session => _session;

    /// <summary>Typed catalog for the open document, when one was parsed.</summary>
    public PdfCatalog? Catalog => _session?.Catalog;

    /// <summary>Reader diagnostics from the current session.</summary>
    public IReadOnlyList<PdfDiagnosticEntry> Diagnostics =>
        _session?.Document.Diagnostics ?? [];

    /// <summary>Current page index, zero-based.</summary>
    public int CurrentPageIndex => _pageIndex;

    /// <summary>Current page number, one-based.</summary>
    public int CurrentPageNumber => _pages.Count == 0 ? 0 : _pageIndex + 1;

    /// <summary>Number of resolved pages.</summary>
    public int PageCount => _pages.Count;

    /// <summary>Current zoom multiplier.</summary>
    public double Zoom => _zoom;

    /// <summary>Current display rotation in degrees.</summary>
    public new int Rotation => _rotation;

    /// <summary>Width assigned to the current reading page after Fit.</summary>
    public double PageWidth => ReadingPageWidth;

    /// <summary>Width of the current page so the whole page fits the reading pane.</summary>
    public double ReadingPageWidth
    {
        get => Math.Max(160, _paneWidth);
    }

    private bool ShowPageRail =>
        OperatingSystem.IsWindows() || Width >= 800;

    private double ReadingPaneWidth => Math.Max(160, _paneWidth);

    private double ReadingPaneHeight => Math.Max(160, _paneHeight);

    /// <summary>Opens a PDF request from a picker or operating-system activation.</summary>
    public async Task OpenAsync(
        PdfOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SetBusy(true, "Opening document…");
        try
        {
            await CloseAsync().ConfigureAwait(false);
            await using var input = await StageAsync(
                    "open-stream",
                    () => request.OpenReadAsync(cancellationToken).AsTask())
                .ConfigureAwait(false);
            var buffer = new MemoryStream();
            await StageAsync(
                    "copy-limit",
                    () => CopyWithLimitAsync(
                        input,
                        buffer,
                        _limits.MaximumDocumentBytes,
                        cancellationToken))
                .ConfigureAwait(false);
            buffer.Position = 0;
            var stableId = request.Descriptor.StableId
                ?? Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
            buffer.Position = 0;
            var source = PdfSources.FromStream(buffer, request.Descriptor.DisplayName, stableId);
            _session = await StageAsync(
                    "parse",
                    () => PdfDocumentSession.OpenAsync(source, _limits, cancellationToken).AsTask())
                .ConfigureAwait(false);
            _pages = Stage(
                "page-tree",
                () => PdfPageTree.Resolve(_session.Document, _limits));
            _textSpans = [];
            _pageIndex = 0;
            _zoom = 1;
            _rotation = 0;
            if (_stateStore is not null)
            {
                await StageAsync(
                        "remember-recent",
                        () => _stateStore.RememberAsync(
                                new PdfRecentDocument(
                                    stableId,
                                    request.Descriptor.DisplayName,
                                    request.Descriptor.StableId ?? request.Descriptor.DisplayName,
                                    DateTimeOffset.UtcNow),
                                cancellationToken)
                            .AsTask())
                    .ConfigureAwait(false);
                if (await StageAsync(
                            "load-position",
                            () => _stateStore.LoadPositionAsync(stableId, cancellationToken).AsTask())
                        .ConfigureAwait(false)
                    is { } position)
                {
                    _pageIndex = Math.Clamp(position.PageIndex, 0, Math.Max(0, _pages.Count - 1));
                    _zoom = Math.Clamp(position.Zoom, 0.5, 4);
                    _rotation = NormalizeRotation(position.Rotation);
                }
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _pageRail.ItemsSource = Enumerable.Range(0, _pages.Count).ToArray();
                _readingPage.PageIndex = _pageIndex;
                SyncRailSelection();
                _titleLabel.Text = _session.Document.Info.Title
                    ?? _session.Document.Info.Source.DisplayName;
                ApplyTheme();
                ApplyWorkspaceColumns();
                RefreshFitSize();
                await RenderCurrentPageAsync(cancellationToken).ConfigureAwait(false);
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetStatus("Opening cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            SetStatus("Unable to open this PDF.");
            Error?.Invoke(this, new PdfViewerErrorEventArgs(exception));
            throw;
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Closes the current document and clears the page surface.</summary>
    public async Task CloseAsync()
    {
        if (_session is not null)
            await _session.DisposeAsync().ConfigureAwait(false);
        _session = null;
        _pages = [];
        _pageIndex = 0;
        _textSpans = [];
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _pageRail.ItemsSource = null;
            _readingPage.PageIndex = -1;
            _searchBar.Text = string.Empty;
            _searchStatus.Text = string.Empty;
            _titleLabel.Text = "No document open";
            SetStatus("Choose a local PDF to begin.");
            UpdateToolbar();
        });
    }

    /// <summary>Moves to the requested zero-based page.</summary>
    public Task GoToPageAsync(int pageIndex, CancellationToken cancellationToken = default)
    {
        if (_pages.Count == 0)
            return Task.CompletedTask;
        _pageIndex = Math.Clamp(pageIndex, 0, _pages.Count - 1);
        _readingPage.PageIndex = _pageIndex;
        SyncRailSelection();
        _pageRail.ScrollTo(_pageIndex, position: ScrollToPosition.Center, animate: true);
        RefreshFitSize();
        UpdateToolbar();
        PageChanged?.Invoke(this, EventArgs.Empty);
        return SavePositionAsync(cancellationToken);
    }

    /// <summary>Moves to the next page.</summary>
    public Task GoNextAsync() => GoToPageAsync(_pageIndex + 1);

    /// <summary>Moves to the previous page.</summary>
    public Task GoPreviousAsync() => GoToPageAsync(_pageIndex - 1);

    /// <summary>Increases the page scale.</summary>
    public Task ZoomInAsync() => SetZoomAsync(_zoom + 0.25);

    /// <summary>Decreases the page scale.</summary>
    public Task ZoomOutAsync() => SetZoomAsync(_zoom - 0.25);

    /// <summary>Fits the current page entirely inside the reading pane.</summary>
    public async Task FitAsync()
    {
        _zoom = 1;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ReloadReadingPage();
            _ = _readingScroll.ScrollToAsync(0, 0, false);
        }).ConfigureAwait(false);
        await SavePositionAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Sets a bounded page scale relative to Fit.</summary>
    public async Task SetZoomAsync(double zoom, CancellationToken cancellationToken = default)
    {
        _zoom = Math.Clamp(zoom, 0.5, 4);
        await MainThread.InvokeOnMainThreadAsync(ReloadReadingPage).ConfigureAwait(false);
        await SavePositionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Handles common reader keyboard commands without leaking OS key types.</summary>
    public bool TryHandleKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || _isBusy || _pages.Count == 0)
            return false;

        switch (key)
        {
            case "Left" or "PageUp":
                _ = GoPreviousAsync();
                return true;
            case "Right" or "PageDown":
                _ = GoNextAsync();
                return true;
            case "OemPlus" or "Add":
                _ = ZoomInAsync();
                return true;
            case "OemMinus" or "Subtract":
                _ = ZoomOutAsync();
                return true;
            case "F":
                _ = FitAsync();
                return true;
            case "R":
                _ = RotateAsync();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Rotates the page clockwise by 90 degrees.</summary>
    public async Task RotateAsync(CancellationToken cancellationToken = default)
    {
        _rotation = NormalizeRotation(_rotation + 90);
        var items = _pageRail.ItemsSource;
        var pageIndex = _readingPage.PageIndex;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _readingPage.PageIndex = -1;
            _readingPage.PageIndex = pageIndex;
            _pageRail.ItemsSource = null;
            _pageRail.ItemsSource = items;
            SyncRailSelection();
            RefreshFitSize();
        });
        await SavePositionAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task RenderCurrentPageAsync(CancellationToken cancellationToken)
    {
        if (_session is null || _pages.Count == 0)
            return Task.CompletedTask;

        SetStatus($"Page {_pageIndex + 1} of {_pages.Count}");
        _readingPage.PageIndex = _pageIndex;
        SyncRailSelection();
        _pageRail.ScrollTo(_pageIndex, position: ScrollToPosition.Center, animate: false);
        UpdateToolbar();
        PageChanged?.Invoke(this, EventArgs.Empty);
        return SavePositionAsync(cancellationToken);
    }

    private Task<PdfRenderedPage> RenderThumbnailAsync(
        int pageIndex,
        CancellationToken cancellationToken) =>
        RenderPageAsync(pageIndex, ThumbnailDpi, cancellationToken);

    private Task<PdfRenderedPage> RenderReadingAsync(
        int pageIndex,
        CancellationToken cancellationToken)
    {
        if (_session is null || pageIndex < 0 || pageIndex >= _pages.Count)
            throw new InvalidOperationException("No PDF document is open.");
        var width = (int)Math.Clamp(ReadingPaneWidth, 1, 16_384);
        var height = (int)Math.Clamp(ReadingPaneHeight, 1, 16_384);
        return Task.FromResult(_renderer.RenderViewport(
            _session.Document,
            _pages[pageIndex],
            width,
            height,
            _zoom,
            _rotation,
            cancellationToken));
    }

    private async Task<PdfRenderedPage> RenderPageAsync(
        int pageIndex,
        double dotsPerInch,
        CancellationToken cancellationToken)
    {
        if (_session is null)
            throw new InvalidOperationException("No PDF document is open.");
        return await StageAsync(
                $"render-page-{pageIndex}",
                () => _renderer.RenderAsync(
                        _session.Document,
                        new PdfRenderRequest(
                            pageIndex,
                            dotsPerInch,
                            Rotation: _rotation),
                        cancellationToken)
                    .AsTask())
            .ConfigureAwait(false);
    }

    private void RefreshFitSize()
    {
        OnPropertyChanged(nameof(ReadingPageWidth));
        OnPropertyChanged(nameof(PageWidth));
    }

    private void ApplyWorkspaceColumns()
    {
        var showRail = ShowPageRail;
        _pageRail.IsVisible = showRail;
        _workspace.ColumnDefinitions[0].Width = showRail
            ? new GridLength(RailWidth)
            : new GridLength(0);
    }

    private void SyncRailSelection()
    {
        _railSync = true;
        _pageRail.SelectedItem = _pages.Count == 0 ? null : _pageIndex;
        _railSync = false;
    }

    private void OnRailSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_railSync || args.CurrentSelection.FirstOrDefault() is not int pageIndex)
            return;
        _ = GoToPageAsync(pageIndex);
    }

    private void OnPageRenderFailed(Exception exception)
    {
        SetStatus("A page could not be rendered.");
        Error?.Invoke(this, new PdfViewerErrorEventArgs(exception));
    }

    private async void GoToEnteredPageAsync()
    {
        if (int.TryParse(_pageEntry.Text, out var pageNumber))
            await GoToPageAsync(pageNumber - 1).ConfigureAwait(false);
        _pageEntry.Text = string.Empty;
    }

    private async void SearchAsync()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_searchBar.Text))
            return;
        try
        {
            _textSpans = await Task.Run(
                    () => _textExtractor.ExtractDocument(_session.Document))
                .ConfigureAwait(false);
            var matches = _textSearch.Search(_textSpans, _searchBar.Text);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _searchStatus.Text = matches.Count == 0
                    ? "No matches"
                    : $"{matches.Count} match{(matches.Count == 1 ? string.Empty : "es")}";
                if (matches.Count > 0)
                    _ = GoToPageAsync(matches[0].PageIndex);
            });
        }
        catch (Exception exception) when (exception is InvalidDataException or PdfReaderException)
        {
            _searchStatus.Text = "Search unavailable";
            Error?.Invoke(this, new PdfViewerErrorEventArgs(exception));
        }
    }

    private async Task SavePositionAsync(CancellationToken cancellationToken)
    {
        if (_stateStore is null || _session is null)
            return;
        var stableId = _session.Source.Descriptor.StableId;
        if (string.IsNullOrWhiteSpace(stableId))
            return;
        await _stateStore.SavePositionAsync(
                new PdfReadingPosition(stableId, _pageIndex, _zoom, _rotation),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs args)
    {
        if (args.Status is GestureStatus.Started)
            _pinchStart = _zoom;
        if (args.Status is GestureStatus.Running)
            _ = SetZoomAsync(_pinchStart * args.Scale);
    }

    /// <inheritdoc />
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width < 32 || height < 32)
            return;
        ApplyWorkspaceColumns();
        var paneWidth = Math.Max(160, width - (ShowPageRail ? RailWidth + 24 : 24));
        var paneHeight = Math.Max(160, height - 148);
        if (Math.Abs(paneWidth - _paneWidth) < 1 && Math.Abs(paneHeight - _paneHeight) < 1)
            return;
        _paneWidth = paneWidth;
        _paneHeight = paneHeight;
        RefreshFitSize();
        ReloadReadingPage();
    }

    private void ReloadReadingPage()
    {
        if (_readingPage.PageIndex < 0)
            return;
        var index = _readingPage.PageIndex;
        _readingPage.PageIndex = -1;
        _readingPage.PageIndex = index;
        UpdateToolbar();
    }

    private void ApplyTheme()
    {
        BackgroundColor = Profile.Background;
        _readingHost.BackgroundColor = Profile.Background;
        _pageRail.BackgroundColor = Profile.Surface;
        _titleLabel.TextColor = Profile.Text;
        _statusLabel.TextColor = Profile.Muted;
        foreach (var button in new[]
                 {
                     _previousButton,
                     _nextButton,
                     _zoomOutButton,
                     _zoomInButton,
                     _fitButton,
                     _rotateButton,
                 })
        {
            button.BackgroundColor = Profile.Action;
            button.TextColor = Profile.OnAction;
        }
        _pageEntry.TextColor = Profile.Text;
        _searchBar.TextColor = Profile.Text;
        _searchBar.BackgroundColor = Profile.Raised;
        _searchStatus.TextColor = Profile.Muted;
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _isBusy = busy;
        if (MainThread.IsMainThread)
            ApplyBusy(busy, status);
        else
            MainThread.BeginInvokeOnMainThread(() => ApplyBusy(busy, status));
    }

    private void ApplyBusy(bool busy, string? status)
    {
        if (status is not null)
            _statusLabel.Text = status;
        UpdateToolbar();
    }

    private void SetStatus(string status)
    {
        if (MainThread.IsMainThread)
            _statusLabel.Text = status;
        else
            MainThread.BeginInvokeOnMainThread(() => _statusLabel.Text = status);
    }

    private static T Stage<T>(string stage, Func<T> work)
    {
        try
        {
            return work();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw WrapStage(stage, exception);
        }
    }

    private static async Task<T> StageAsync<T>(string stage, Func<Task<T>> work)
    {
        try
        {
            return await work().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw WrapStage(stage, exception);
        }
    }

    private static Task StageAsync(string stage, Func<Task> work) =>
        StageAsync(stage, async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        });

    private static PdfReaderException WrapStage(string stage, Exception exception) =>
        new(
            new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Error,
                "PDFOPEN",
                $"Open failed at {stage}: {exception.Message}",
                Exception: exception));

    private void UpdateToolbar()
    {
        var enabled = !_isBusy && _pages.Count > 0;
        _previousButton.IsEnabled = enabled && _pageIndex > 0;
        _nextButton.IsEnabled = enabled && _pageIndex < _pages.Count - 1;
        _zoomOutButton.IsEnabled = enabled && _zoom > 0.5;
        _zoomInButton.IsEnabled = enabled && _zoom < 4;
        _fitButton.IsEnabled = enabled;
        _rotateButton.IsEnabled = enabled;
        _pageLabel.Text = $"{(_pages.Count == 0 ? 0 : _pageIndex + 1)} / {_pages.Count}";
    }

    private static Button CreateToolbarButton(
        string text,
        string automationId,
        Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            AutomationId = automationId,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 14,
            Padding = new Thickness(12, 7),
        };
        SemanticProperties.SetDescription(button, text);
        button.Clicked += async (_, _) => await action().ConfigureAwait(false);
        return button;
    }

    private static Label CreateLabel(string text, string automationId) =>
        new()
        {
            Text = text,
            AutomationId = automationId,
            FontSize = 13,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
        };

    private static async Task CopyWithLimitAsync(
        Stream input,
        Stream output,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0)
                return;
            total += count;
            if (total > maximumBytes)
                throw new InvalidDataException($"The PDF exceeds the {maximumBytes} byte reader limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
    }

    private static int NormalizeRotation(int rotation) =>
        ((rotation % 360) + 360) % 360;
}
