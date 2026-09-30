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
    private readonly Button _fitWidthButton;
    private readonly Button _pagesTabButton;
    private readonly Button _outlineTabButton;
    private readonly Button _rotateButton;
    private readonly Label _pageLabel;
    private readonly Entry _pageEntry;
    private readonly SearchBar _searchBar;
    private readonly Label _searchStatus;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly CollectionView _pageRail;
    private readonly CollectionView _outlineList;
    private readonly CollectionView _readingStack;
    private readonly Grid _sidebar;
    private readonly Grid _workspace;
    private readonly PdfNavigationExtractor _navigation = new();
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
    private PdfFitKind _fitKind = PdfFitKind.Page;
    private bool _showOutline;
    private IReadOnlyList<PdfOutlineItem> _outlines = [];
    private int _firstVisible;
    private int _lastVisible;

    /// <summary>Creates a viewer with optional local reading-position persistence.</summary>
    public PdfViewer(
        IPdfDocumentStateStore? stateStore = null,
        PdfLimits? limits = null)
    {
        AutomationId = "PdfViewer";
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
        _fitWidthButton = CreateToolbarButton("Fit width", "PdfFitWidth", FitWidthAsync);
        _pagesTabButton = CreateToolbarButton("Pages", "PdfPagesTab", () => ShowSidebar(outline: false));
        _outlineTabButton = CreateToolbarButton("Contents", "PdfOutlineTab", () => ShowSidebar(outline: true));
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
        _outlineList = new CollectionView
        {
            AutomationId = "PdfOutline",
            SelectionMode = SelectionMode.Single,
            IsVisible = false,
            BackgroundColor = Profile.Surface,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
        };
        _outlineList.ItemTemplate = new DataTemplate(() =>
        {
            var title = new Label
            {
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap,
                Padding = new Thickness(8, 6),
            };
            title.SetBinding(Label.TextProperty, nameof(PdfOutlineItem.Title));
            return title;
        });
        _outlineList.SelectionChanged += OnOutlineSelectionChanged;
        _sidebar = new Grid
        {
            WidthRequest = RailWidth,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        var sidebarTabs = new HorizontalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(4),
            Children = { _pagesTabButton, _outlineTabButton },
        };
        _sidebar.Add(sidebarTabs, 0, 0);
        _sidebar.Add(_pageRail, 0, 1);
        _sidebar.Add(_outlineList, 0, 1);
        _readingStack = new CollectionView
        {
            AutomationId = "PdfReadingPage",
            SelectionMode = SelectionMode.None,
            BackgroundColor = Profile.Background,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
            {
                ItemSpacing = 12,
            },
        };
        _readingStack.ItemTemplate = new DataTemplate(() =>
        {
            var page = new PdfPageView
            {
                HorizontalOptions = LayoutOptions.Center,
                RenderPageAsync = RenderReadingAsync,
                RenderFailed = OnPageRenderFailed,
            };
            page.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PdfPageView.PageIndex) && page.PageIndex >= 0)
                    page.PageWidth = CellSize(page.PageIndex).Width;
            };
            page.SetBinding(PdfPageView.PageIndexProperty, ".");
            return page;
        });
        _readingStack.Scrolled += OnReadingScrolled;
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        _readingStack.GestureRecognizers.Add(pinch);
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
        _workspace.Add(_sidebar, 0, 0);
        _workspace.Add(_readingStack, 1, 0);
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
                _fitWidthButton,
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
    public double ReadingPageWidth =>
        CellSize(System.Math.Clamp(_pageIndex, 0, System.Math.Max(0, _pages.Count - 1))).Width;

    /// <summary>Focuses the goto-page field (Ctrl+G).</summary>
    public void FocusPageEntry() => _pageEntry.Focus();

    /// <summary>Applies Ctrl+wheel zoom or document scroll.</summary>
    public bool TryHandleWheel(double delta, bool control)
    {
        if (_pages.Count == 0)
            return false;
        if (control)
        {
            _ = SetZoomAsync(_zoom + (delta > 0 ? 0.25 : -0.25));
            return true;
        }

        return false;
    }

    private double ReadingPaneWidth => System.Math.Max(160, _paneWidth);

    private double ReadingPaneHeight => System.Math.Max(160, _paneHeight);

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
                    _pageIndex = System.Math.Clamp(position.PageIndex, 0, System.Math.Max(0, _pages.Count - 1));
                    _zoom = System.Math.Clamp(position.Zoom, 0.5, 4);
                    _rotation = NormalizeRotation(position.Rotation);
                }
            }

            _outlines = _navigation.ExtractOutlines(_session.Document);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var pages = Enumerable.Range(0, _pages.Count).ToArray();
                _pageRail.ItemsSource = pages;
                _readingStack.ItemsSource = pages;
                _outlineList.ItemsSource = _outlines;
                SyncRailSelection();
                _titleLabel.Text = _session.Document.Info.Title
                    ?? _session.Document.Info.Source.DisplayName;
                ApplyTheme();
                ApplyWorkspaceColumns();
                RefreshFitSize();
                await RenderCurrentPageAsync(cancellationToken);
                ScrollPagesIntoView(animate: false);
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
        _outlines = [];
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _pageRail.ItemsSource = null;
            _readingStack.ItemsSource = null;
            _outlineList.ItemsSource = null;
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
        _pageIndex = System.Math.Clamp(pageIndex, 0, _pages.Count - 1);
        if (MainThread.IsMainThread)
            ApplyPageNavigation(animate: true);
        else
            MainThread.BeginInvokeOnMainThread(() => ApplyPageNavigation(animate: true));
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
    public Task FitAsync()
    {
        _fitKind = PdfFitKind.Page;
        return SetZoomAsync(1);
    }

    /// <summary>Fits the current page width to the reading pane.</summary>
    public Task FitWidthAsync()
    {
        _fitKind = PdfFitKind.Width;
        return SetZoomAsync(1);
    }

    /// <summary>Sets a bounded page scale relative to Fit.</summary>
    public async Task SetZoomAsync(double zoom, CancellationToken cancellationToken = default)
    {
        _zoom = System.Math.Clamp(zoom, 0.5, 4);
        await MainThread.InvokeOnMainThreadAsync(ReloadReadingStack).ConfigureAwait(false);
        await SavePositionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Handles common reader keyboard commands without leaking OS key types.</summary>
    public bool TryHandleKey(string key, bool control = false)
    {
        if (string.IsNullOrWhiteSpace(key) || _isBusy || _pages.Count == 0)
            return false;

        if (control && key is "G")
        {
            FocusPageEntry();
            return true;
        }

        switch (key)
        {
            case "Left":
                _ = GoPreviousAsync();
                return true;
            case "Right":
                _ = GoNextAsync();
                return true;
            case "PageUp":
                _ = ScrollViewportAsync(-1);
                return true;
            case "PageDown":
                _ = ScrollViewportAsync(1);
                return true;
            case "Home":
                _ = GoToPageAsync(0);
                return true;
            case "End":
                _ = GoToPageAsync(_pages.Count - 1);
                return true;
            case "OemPlus" or "Add":
                _ = ZoomInAsync();
                return true;
            case "OemMinus" or "Subtract":
                _ = ZoomOutAsync();
                return true;
            case "F":
                _ = control ? FitWidthAsync() : FitAsync();
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
        await MainThread.InvokeOnMainThreadAsync(ReloadReadingStack).ConfigureAwait(false);
        await SavePositionAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task RenderCurrentPageAsync(CancellationToken cancellationToken)
    {
        if (_session is null || _pages.Count == 0)
            return Task.CompletedTask;

        SetStatus($"Page {_pageIndex + 1} of {_pages.Count}");
        SyncRailSelection();
        ScrollPagesIntoView(animate: false);
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
        var cell = CellSize(pageIndex);
        var width = (int)System.Math.Clamp(cell.Width, 1, 16_384);
        var height = (int)System.Math.Clamp(cell.Height, 1, 16_384);
        return Task.FromResult(_renderer.RenderScaled(
            _session.Document,
            _pages[pageIndex],
            width,
            height,
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
        _sidebar.IsVisible = true;
        _workspace.ColumnDefinitions[0].Width = new GridLength(RailWidth);
        _pageRail.IsVisible = !_showOutline;
        _outlineList.IsVisible = _showOutline;
    }

    private Task ShowSidebar(bool outline)
    {
        _showOutline = outline;
        ApplyWorkspaceColumns();
        return Task.CompletedTask;
    }

    private (double Width, double Height) CellSize(int pageIndex)
    {
        if (_pages.Count == 0)
            return (System.Math.Max(160, _paneWidth), System.Math.Max(160, _paneHeight));
        var page = _pages[System.Math.Clamp(pageIndex, 0, _pages.Count - 1)];
        var info = page.Info with { Rotation = page.Info.Rotation + _rotation };
        return PdfPageFit.DisplaySize(
            _paneWidth,
            _paneHeight,
            info.DisplayWidth,
            info.DisplayHeight,
            _zoom,
            _fitKind);
    }

    private Task ScrollViewportAsync(int direction)
    {
        if (_pages.Count == 0)
            return Task.CompletedTask;
        var span = System.Math.Max(1, _lastVisible - _firstVisible);
        if (span <= 1)
        {
            var cell = CellSize(_pageIndex);
            span = System.Math.Max(1, (int)System.Math.Floor(ReadingPaneHeight / System.Math.Max(1, cell.Height + 12)));
        }

        return GoToPageAsync(_pageIndex + (direction * span));
    }

    private void OnReadingScrolled(object? sender, ItemsViewScrolledEventArgs args)
    {
        if (_pages.Count == 0 || args.FirstVisibleItemIndex < 0)
            return;
        _firstVisible = System.Math.Clamp(args.FirstVisibleItemIndex, 0, _pages.Count - 1);
        _lastVisible = System.Math.Clamp(System.Math.Max(args.LastVisibleItemIndex, _firstVisible), 0, _pages.Count - 1);
        var visible = _firstVisible;
        if (visible == _pageIndex)
            return;
        _pageIndex = visible;
        SyncRailSelection();
        UpdateToolbar();
        SetStatus($"Page {_pageIndex + 1} of {_pages.Count}");
        PageChanged?.Invoke(this, EventArgs.Empty);
        _ = SavePositionAsync(CancellationToken.None);
    }

    private void OnOutlineSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (args.CurrentSelection.FirstOrDefault() is not PdfOutlineItem item)
            return;
        if (item.PageIndex is int pageIndex)
            _ = GoToPageAsync(pageIndex);
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
        var paneWidth = System.Math.Max(160, width - RailWidth - 24);
        var paneHeight = System.Math.Max(160, height - 148);
        if (System.Math.Abs(paneWidth - _paneWidth) < 1 && System.Math.Abs(paneHeight - _paneHeight) < 1)
            return;
        _paneWidth = paneWidth;
        _paneHeight = paneHeight;
        RefreshFitSize();
        ReloadReadingStack();
    }

    private void ReloadReadingStack()
    {
        if (_pages.Count == 0)
            return;
        var pages = Enumerable.Range(0, _pages.Count).ToArray();
        _readingStack.ItemsSource = null;
        _readingStack.ItemsSource = pages;
        _pageRail.ItemsSource = null;
        _pageRail.ItemsSource = pages;
        SyncRailSelection();
        ScrollPagesIntoView(animate: false);
        UpdateToolbar();
    }

    private void ApplyPageNavigation(bool animate)
    {
        SyncRailSelection();
        ScrollPagesIntoView(animate);
        RefreshFitSize();
        UpdateToolbar();
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ScrollPagesIntoView(bool animate)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => ScrollPagesIntoView(animate));
            return;
        }

        if (_pages.Count == 0)
            return;
        _pageRail.ScrollTo(_pageIndex, position: ScrollToPosition.Center, animate: animate);
        _readingStack.ScrollTo(_pageIndex, position: ScrollToPosition.Start, animate: animate);
    }

    private void ApplyTheme()
    {
        BackgroundColor = Profile.Background;
        _readingStack.BackgroundColor = Profile.Background;
        _pageRail.BackgroundColor = Profile.Surface;
        _outlineList.BackgroundColor = Profile.Surface;
        _sidebar.BackgroundColor = Profile.Surface;
        _titleLabel.TextColor = Profile.Text;
        _statusLabel.TextColor = Profile.Muted;
        foreach (var button in new[]
                 {
                     _previousButton,
                     _nextButton,
                     _zoomOutButton,
                     _zoomInButton,
                     _fitButton,
                     _fitWidthButton,
                     _pagesTabButton,
                     _outlineTabButton,
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
        _fitWidthButton.IsEnabled = enabled;
        _pagesTabButton.IsEnabled = enabled;
        _outlineTabButton.IsEnabled = enabled;
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
