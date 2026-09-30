using System.Security.Cryptography;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Layouts;
using Novolis.Maui.GraphicalProfile;
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
    private FlexLayout? _toolbar;
    private readonly PdfLimits _limits;
    private readonly IPdfDocumentStateStore? _stateStore;
    private readonly PdfSkiaPageRenderer _renderer;
    private readonly Button _openButton;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _zoomOutButton;
    private readonly Button _zoomInButton;
    private readonly Button _fitButton;
    private readonly Button _fitWidthButton;
    private readonly Button _pagesTabButton;
    private readonly Button _outlineTabButton;
    private readonly Button _rotateButton;
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly Label _pageLabel;
    private readonly Entry _pageEntry;
    private readonly SearchBar _searchBar;
    private readonly Label _searchStatus;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly CollectionView _pageRail;
    private readonly CollectionView _outlineList;
    private readonly ScrollView _readingScroll;
    private readonly VerticalStackLayout _readingStrip;
    private readonly List<Grid> _readingSlots = [];
    private readonly List<PdfPageView> _readingPages = [];
    private readonly Grid _sidebar;
    private readonly Grid _workspace;
    private readonly Grid _viewerHeader;
    private readonly BoxView _sidebarScrim;
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
    private bool _fitUserChosen;
    private bool _reloadingStack;
    private bool _syncingScroll;
    private int _windowStart;
    private IDispatcherTimer? _scrollSettle;
    private bool _showOutline;
    private bool _sidebarOpen;
    private IReadOnlyList<PdfOutlineItem> _outlines = [];

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

        _openButton = CreateToolbarButton("Open", "PdfViewerOpen", RequestOpenAsync);
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
            ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
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
                HorizontalOptions = LayoutOptions.Center,
                RenderPageAsync = RenderThumbnailAsync,
                RenderFailed = OnPageRenderFailed,
            };
            page.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(PdfPageView.PageIndex) || page.PageIndex < 0)
                    return;
                var thumb = ThumbSize(page.PageIndex);
                page.PageWidth = thumb.Width;
                page.PageHeight = thumb.Height;
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
            BackgroundColor = Profile.Surface,
        };
        _sidebar.Add(_pageRail);
        _sidebar.Add(_outlineList);
        _sidebarScrim = new BoxView
        {
            Color = Profile.Text.WithAlpha(0.28f),
            IsVisible = false,
        };
        _sidebarScrim.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(CloseCompactSidebar),
        });
        _readingStrip = new VerticalStackLayout
        {
            Spacing = 0,
            BackgroundColor = Profile.Background,
        };
        for (var slot = 0; slot < PdfPageTurn.WindowSize; slot++)
            _readingStrip.Add(CreateReadingSlot());
        _readingScroll = new ScrollView
        {
            AutomationId = "PdfReadingPage",
            BackgroundColor = Profile.Background,
            Orientation = ScrollOrientation.Vertical,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            VerticalScrollBarVisibility = ScrollBarVisibility.Default,
            Content = _readingStrip,
        };
        _readingScroll.SizeChanged += (_, _) => UpdatePaneFromReadingStack();
        _readingScroll.Scrolled += OnReadingScrolled;
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        _readingScroll.GestureRecognizers.Add(pinch);
        _scrollSettle = Dispatcher.CreateTimer();
        _scrollSettle.Interval = TimeSpan.FromMilliseconds(140);
        _scrollSettle.IsRepeating = false;
        _scrollSettle.Tick += (_, _) => _ = SettleReadingScrollAsync();
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
        _workspace.Add(_readingScroll, 1, 0);
        _workspace.Add(_sidebarScrim, 0, 0);
        _workspace.Add(_sidebar, 0, 0);
        SizeChanged += (_, _) => ApplyWorkspaceColumns();

        _toolbar = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Center,
            AlignContent = FlexAlignContent.Start,
            Padding = new Thickness(8, 6),
            Children =
            {
                _openButton,
                _pagesTabButton,
                _outlineTabButton,
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

        _viewerHeader = new Grid
        {
            Padding = new Thickness(16, 14, 16, 8),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
        };
        _viewerHeader.Add(_titleLabel, 0, 0);
        _viewerHeader.Add(_statusLabel, 0, 1);

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
        layout.Add(_viewerHeader, 0, 0);
        layout.Add(_toolbar, 0, 1);
        layout.Add(_workspace, 0, 2);
        Content = layout;

        ApplyWorkspaceColumns();
        ApplyTheme();
        UpdateToolbar();
    }

    /// <summary>Raised when the compact Open control is used.</summary>
    public event EventHandler? OpenRequested;

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

    private Task RequestOpenAsync()
    {
        OpenRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <summary>Recomputes phone vs desktop chrome after the host shows the viewer.</summary>
    public void RefreshChrome()
    {
        ApplyWorkspaceColumns();
        UpdatePaneFromReadingStack();
    }

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

    private static double ScreenWidth
    {
        get
        {
            var info = DeviceDisplay.MainDisplayInfo;
            var width = info.Density > 0 ? info.Width / info.Density : 0;
            return width > 32 ? width : 360;
        }
    }

    private static double ScreenHeight
    {
        get
        {
            var info = DeviceDisplay.MainDisplayInfo;
            var height = info.Density > 0 ? info.Height / info.Density : 0;
            return height > 32 ? height : 640;
        }
    }

    private bool IsPhone => DeviceInfo.Idiom == DeviceIdiom.Phone;

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
            _fitUserChosen = false;
            _fitKind = PdfFitKind.Page;
            _sidebarOpen = false;
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
                ShowReadingPage();
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
            ClearReadingWindow();
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
        _fitUserChosen = true;
        _fitKind = PdfFitKind.Page;
        return SetZoomAsync(1);
    }

    /// <summary>Fits the current page width to the reading pane.</summary>
    public Task FitWidthAsync()
    {
        _fitUserChosen = true;
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

    private async Task<PdfRenderedPage> RenderReadingAsync(
        int pageIndex,
        CancellationToken cancellationToken)
    {
        if (_session is null || pageIndex < 0 || pageIndex >= _pages.Count)
            throw new InvalidOperationException("No PDF document is open.");
        var cell = CellSize(pageIndex);
        var width = (int)System.Math.Clamp(cell.Width, 1, 16_384);
        var height = (int)System.Math.Clamp(cell.Height, 1, 16_384);
        var document = _session.Document;
        var page = _pages[pageIndex];
        var rotation = _rotation;
        await _renderGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                    () => _renderer.RenderScaled(
                        document,
                        page,
                        width,
                        height,
                        rotation,
                        cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _renderGate.Release();
        }
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

    private bool IsCompact => PdfViewerLayout.IsCompact(Width);

    private bool ApplyDefaultFit()
    {
        if (_fitUserChosen)
            return false;
        var next = IsCompact ? PdfFitKind.Width : PdfFitKind.Page;
        if (next == _fitKind)
            return false;
        _fitKind = next;
        return true;
    }

    private void CloseCompactSidebar()
    {
        if (!IsCompact)
            return;
        _sidebarOpen = false;
        ApplyWorkspaceColumns();
    }

    private void ApplyWorkspaceColumns()
    {
        var compact = IsCompact;
        if (_toolbar is not null)
        {
            var toolbarWidth = Width > 32 ? Width : ScreenWidth;
            _toolbar.WidthRequest = System.Math.Max(160, toolbarWidth);
        }
        _viewerHeader.IsVisible = !compact;
        _openButton.IsVisible = compact;
        _searchBar.IsVisible = !compact;
        _pageEntry.IsVisible = !compact;
        _searchStatus.IsVisible = !compact;
        _fitWidthButton.IsVisible = !compact;
        _rotateButton.IsVisible = !compact;
        _previousButton.Text = compact ? "Prev" : "Previous";
        _zoomOutButton.Text = compact ? "−" : "Zoom −";
        _zoomInButton.Text = compact ? "+" : "Zoom +";
        foreach (var button in new[]
                 {
                     _openButton,
                     _pagesTabButton,
                     _outlineTabButton,
                     _previousButton,
                     _nextButton,
                     _zoomOutButton,
                     _zoomInButton,
                     _fitButton,
                     _fitWidthButton,
                     _rotateButton,
                 })
        {
            button.Padding = compact ? new Thickness(10, 6) : new Thickness(12, 8);
            button.MinimumHeightRequest = compact ? 40 : GraphicalProfileColors.TouchTarget;
            button.FontSize = compact ? 12 : GraphicalProfileColors.NavigationSize;
        }
        _workspace.Padding = compact ? new Thickness(0) : new Thickness(8, 0, 8, 8);
        _workspace.ColumnSpacing = compact ? 0 : 8;
        if (compact)
        {
            _workspace.ColumnDefinitions[0].Width = GridLength.Star;
            _workspace.ColumnDefinitions[1].Width = new GridLength(0);
            Grid.SetColumn(_readingScroll, 0);
            Grid.SetColumnSpan(_readingScroll, 2);
            Grid.SetColumn(_sidebarScrim, 0);
            Grid.SetColumnSpan(_sidebarScrim, 2);
            Grid.SetColumn(_sidebar, 0);
            Grid.SetColumnSpan(_sidebar, 2);
            _sidebar.HorizontalOptions = LayoutOptions.Start;
            _sidebar.WidthRequest = RailWidth;
            _sidebar.IsVisible = _sidebarOpen;
            _sidebarScrim.IsVisible = _sidebarOpen;
        }
        else
        {
            _workspace.ColumnDefinitions[0].Width = new GridLength(RailWidth);
            _workspace.ColumnDefinitions[1].Width = GridLength.Star;
            Grid.SetColumn(_readingScroll, 1);
            Grid.SetColumnSpan(_readingScroll, 1);
            Grid.SetColumn(_sidebar, 0);
            Grid.SetColumnSpan(_sidebar, 1);
            _sidebar.HorizontalOptions = LayoutOptions.Fill;
            _sidebar.WidthRequest = RailWidth;
            _sidebar.IsVisible = true;
            _sidebarScrim.IsVisible = false;
        }

        _pageRail.IsVisible = !_showOutline;
        _outlineList.IsVisible = _showOutline;
        if (ApplyDefaultFit() && _pages.Count > 0)
        {
            RefreshFitSize();
            ReloadReadingStack();
        }

        ApplyNavButtonTheme();
    }

    private Task ShowSidebar(bool outline)
    {
        if (IsCompact && _sidebarOpen && _showOutline == outline)
            _sidebarOpen = false;
        else
        {
            _showOutline = outline;
            _sidebarOpen = true;
        }

        ApplyWorkspaceColumns();
        return Task.CompletedTask;
    }

    private (double Width, double Height) CellSize(int pageIndex)
    {
        var media = MediaSize(pageIndex);
        return PdfPageFit.DisplaySize(
            _paneWidth,
            _paneHeight,
            media.Width,
            media.Height,
            _zoom,
            _fitKind);
    }

    private (double Width, double Height) ThumbSize(int pageIndex)
    {
        const double width = 128;
        var media = MediaSize(pageIndex);
        var height = width * media.Height / media.Width;
        return (width, System.Math.Clamp(height, 80, 4096));
    }

    private (double Width, double Height) MediaSize(int pageIndex)
    {
        if (_pages.Count == 0)
            return (432, 648);
        var page = _pages[System.Math.Clamp(pageIndex, 0, _pages.Count - 1)];
        var info = page.Info with { Rotation = page.Info.Rotation + _rotation };
        var width = info.DisplayWidth > 0 && double.IsFinite(info.DisplayWidth) ? info.DisplayWidth : 432;
        var height = info.DisplayHeight > 0 && double.IsFinite(info.DisplayHeight) ? info.DisplayHeight : 648;
        return (width, height);
    }

    private Task ScrollViewportAsync(int direction)
    {
        if (_pages.Count == 0)
            return Task.CompletedTask;
        var y = System.Math.Max(0, _readingScroll.ScrollY + (direction * ReadingPaneHeight * 0.92));
        return _readingScroll.ScrollToAsync(0, y, animated: true);
    }

    private void OnOutlineSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (args.CurrentSelection.FirstOrDefault() is not PdfOutlineItem item)
            return;
        if (item.PageIndex is int pageIndex)
        {
            CloseCompactSidebar();
            _ = GoToPageAsync(pageIndex);
        }
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
        CloseCompactSidebar();
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
        UpdatePaneFromReadingStack();
    }

    private void UpdatePaneFromReadingStack()
    {
        var compact = IsCompact;
        var rail = compact ? 0 : RailWidth;
        var pad = compact ? 0 : 16;
        var viewerWidth = Width > 32 ? Width : ScreenWidth;
        var viewerHeight = Height > 32 ? Height : ScreenHeight;
        var allocatedWidth = System.Math.Max(160, viewerWidth - rail - pad);
        var chrome = compact ? 72 : 148;
        var allocatedHeight = System.Math.Max(160, viewerHeight - chrome);
        var paneWidth = allocatedWidth;
        var paneHeight = _readingScroll.Height > 32 && _readingScroll.Height <= viewerHeight
            ? _readingScroll.Height
            : allocatedHeight;
        paneWidth = System.Math.Max(160, paneWidth);
        paneHeight = System.Math.Max(160, paneHeight);
        var fitChanged = ApplyDefaultFit();
        if (!fitChanged
            && System.Math.Abs(paneWidth - _paneWidth) < 1
            && System.Math.Abs(paneHeight - _paneHeight) < 1)
            return;
        _paneWidth = paneWidth;
        _paneHeight = paneHeight;
        RefreshFitSize();
        ReloadReadingStack();
    }

    private void ReloadReadingStack()
    {
        if (_pages.Count == 0 || _reloadingStack)
            return;
        _reloadingStack = true;
        try
        {
            ReloadReadingStackCore();
        }
        finally
        {
            _reloadingStack = false;
        }
    }

    private void ReloadReadingStackCore()
    {
        var pages = Enumerable.Range(0, _pages.Count).ToArray();
        ShowReadingPage();
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
        _ = NavigateReadingAsync(animate);
    }

    private void ShowReadingPage()
    {
        if (_pages.Count == 0)
        {
            ClearReadingWindow();
            UpdateToolbar();
            return;
        }

        ReloadReadingWindow();
        _ = NavigateReadingAsync(animate: false);
        UpdateToolbar();
    }

    private Grid CreateReadingSlot()
    {
        var page = new PdfPageView
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            RenderPageAsync = RenderReadingAsync,
            RenderFailed = OnPageRenderFailed,
        };
        var slot = new Grid
        {
            BackgroundColor = Profile.Background,
            HorizontalOptions = LayoutOptions.Fill,
        };
        slot.Add(page);
        _readingPages.Add(page);
        _readingSlots.Add(slot);
        return slot;
    }

    private double SlotHeight
    {
        get
        {
            if (_pages.Count == 0)
                return ReadingPaneHeight;
            var cell = CellSize(_pageIndex);
            return System.Math.Max(1, cell.Height + PdfPageTurn.PageGutter);
        }
    }

    private void ClearReadingWindow()
    {
        _windowStart = 0;
        foreach (var page in _readingPages)
            page.PageIndex = -1;
        foreach (var slot in _readingSlots)
            slot.IsVisible = false;
    }

    private void BindReadingSlot(Grid slot, PdfPageView page, int pageIndex)
    {
        var slotWidth = ReadingPaneWidth;
        slot.BackgroundColor = Profile.Background;
        slot.HorizontalOptions = LayoutOptions.Fill;
        slot.WidthRequest = slotWidth;
        slot.MinimumWidthRequest = slotWidth;
        if (OperatingSystem.IsWindows())
            slot.MaximumWidthRequest = slotWidth;
        else
            slot.ClearValue(Grid.MaximumWidthRequestProperty);
        if (pageIndex < 0 || pageIndex >= _pages.Count)
        {
            page.PageIndex = -1;
            slot.IsVisible = false;
            slot.HeightRequest = SlotHeight;
            slot.MinimumHeightRequest = slot.HeightRequest;
            if (OperatingSystem.IsWindows())
                slot.MaximumHeightRequest = slot.HeightRequest;
            else
                slot.ClearValue(Grid.MaximumHeightRequestProperty);
            return;
        }

        slot.IsVisible = true;
        var cell = CellSize(pageIndex);
        var height = cell.Height + PdfPageTurn.PageGutter;
        slot.HeightRequest = height;
        slot.MinimumHeightRequest = height;
        if (OperatingSystem.IsWindows())
            slot.MaximumHeightRequest = height;
        else
            slot.ClearValue(Grid.MaximumHeightRequestProperty);
        page.HorizontalOptions = LayoutOptions.Center;
        page.VerticalOptions = LayoutOptions.Start;
        page.PageWidth = cell.Width;
        page.PageHeight = cell.Height;
        if (page.PageIndex != pageIndex)
            page.PageIndex = pageIndex;
    }

    private void UpdateStripHeight()
    {
        var height = 0d;
        var visible = 0;
        foreach (var slot in _readingSlots)
        {
            if (!slot.IsVisible)
                continue;
            visible++;
            height += slot.HeightRequest > 0 ? slot.HeightRequest : SlotHeight;
        }

        var fallback = System.Math.Max(SlotHeight, visible * SlotHeight);
        var stripHeight = height > 0 ? height : fallback;
        _readingStrip.WidthRequest = ReadingPaneWidth;
        _readingStrip.MinimumWidthRequest = ReadingPaneWidth;
        _readingStrip.HeightRequest = stripHeight;
        _readingStrip.MinimumHeightRequest = stripHeight;
        _readingStrip.HorizontalOptions = LayoutOptions.Fill;
        if (OperatingSystem.IsWindows())
        {
            _readingStrip.MaximumWidthRequest = ReadingPaneWidth;
            _readingStrip.MaximumHeightRequest = stripHeight;
        }
        else
        {
            _readingStrip.ClearValue(MaximumWidthRequestProperty);
            _readingStrip.ClearValue(MinimumHeightRequestProperty);
            _readingStrip.ClearValue(MaximumHeightRequestProperty);
            _readingStrip.MinimumHeightRequest = stripHeight;
        }
    }

    private void ReloadReadingWindow()
    {
        if (_pages.Count == 0)
        {
            ClearReadingWindow();
            return;
        }

        _windowStart = PdfPageTurn.WindowStart(_pageIndex, _pages.Count);
        _readingScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Default;
        for (var i = 0; i < _readingSlots.Count; i++)
            BindReadingSlot(_readingSlots[i], _readingPages[i], _windowStart + i);
        UpdateStripHeight();
    }

    private bool ReadingWindowContains(int pageIndex) =>
        _readingPages.Count > 0
        && _readingPages[0].PageIndex >= 0
        && pageIndex >= _windowStart
        && pageIndex < _windowStart + _readingSlots.Count
        && pageIndex < _pages.Count;

    private async Task NavigateReadingAsync(bool animate)
    {
        if (_pages.Count == 0)
        {
            ClearReadingWindow();
            return;
        }

        var alreadyShowing = ReadingWindowContains(_pageIndex);
        _syncingScroll = true;
        try
        {
            if (!alreadyShowing)
                ReloadReadingWindow();
            var y = PdfPageTurn.SlotOffset(_pageIndex, _windowStart, SlotHeight);
            await _readingScroll.ScrollToAsync(0, y, animated: animate && alreadyShowing).ConfigureAwait(true);
        }
        finally
        {
            _syncingScroll = false;
        }

        await EnsureReadingWindowAsync().ConfigureAwait(true);
    }

    private async Task ShiftForwardAsync()
    {
        var nextIndex = _windowStart + _readingSlots.Count;
        if (nextIndex >= _pages.Count || _readingSlots.Count == 0)
            return;
        var slot = _readingSlots[0];
        var page = _readingPages[0];
        var removedHeight = System.Math.Max(1, slot.HeightRequest);
        var scrollY = _readingScroll.ScrollY;
        _readingStrip.Remove(slot);
        _readingSlots.RemoveAt(0);
        _readingPages.RemoveAt(0);
        _windowStart++;
        _readingStrip.Add(slot);
        _readingSlots.Add(slot);
        _readingPages.Add(page);
        BindReadingSlot(slot, page, nextIndex);
        UpdateStripHeight();
        await _readingScroll.ScrollToAsync(0, System.Math.Max(0, scrollY - removedHeight), animated: false)
            .ConfigureAwait(true);
    }

    private async Task ShiftBackwardAsync()
    {
        if (_windowStart <= 0 || _readingSlots.Count == 0)
            return;
        var previousIndex = _windowStart - 1;
        var last = _readingSlots.Count - 1;
        var slot = _readingSlots[last];
        var page = _readingPages[last];
        var addedHeight = System.Math.Max(1, SlotHeight);
        var scrollY = _readingScroll.ScrollY;
        _readingStrip.Remove(slot);
        _readingSlots.RemoveAt(last);
        _readingPages.RemoveAt(last);
        _windowStart--;
        BindReadingSlot(slot, page, previousIndex);
        addedHeight = System.Math.Max(1, slot.HeightRequest);
        _readingStrip.Insert(0, slot);
        _readingSlots.Insert(0, slot);
        _readingPages.Insert(0, page);
        UpdateStripHeight();
        await _readingScroll.ScrollToAsync(0, scrollY + addedHeight, animated: false).ConfigureAwait(true);
    }

    private async Task EnsureReadingWindowAsync()
    {
        if (_pages.Count == 0 || _syncingScroll)
            return;
        _syncingScroll = true;
        try
        {
            for (var step = 0; step < PdfPageTurn.WindowSize; step++)
            {
                var pageIndex = PdfPageTurn.PageIndexAt(
                    _readingScroll.ScrollY,
                    _windowStart,
                    _pages.Count,
                    SlotHeight);
                if (PdfPageTurn.NeedsForwardShift(pageIndex, _windowStart, _pages.Count, _readingSlots.Count))
                    await ShiftForwardAsync().ConfigureAwait(true);
                else if (PdfPageTurn.NeedsBackwardShift(pageIndex, _windowStart))
                    await ShiftBackwardAsync().ConfigureAwait(true);
                else
                    break;
            }
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    private void OnReadingScrolled(object? sender, ScrolledEventArgs args)
    {
        if (_syncingScroll || _pages.Count == 0 || _reloadingStack)
            return;
        UpdateReadingPageFromScroll(args.ScrollY);
        _ = EnsureReadingWindowAsync();
        _scrollSettle?.Stop();
        _scrollSettle?.Start();
    }

    private void UpdateReadingPageFromScroll(double scrollY)
    {
        var pageIndex = PdfPageTurn.PageIndexAt(scrollY, _windowStart, _pages.Count, SlotHeight);
        if (pageIndex != _pageIndex)
        {
            _pageIndex = pageIndex;
            SyncRailSelection();
            UpdateToolbar();
            PageChanged?.Invoke(this, EventArgs.Empty);
            _ = SavePositionAsync(CancellationToken.None);
        }
    }

    private Task SettleReadingScrollAsync() => EnsureReadingWindowAsync();

    private void ApplyTheme()
    {
        BackgroundColor = Profile.Background;
        _readingScroll.BackgroundColor = Profile.Background;
        _readingStrip.BackgroundColor = Profile.Background;
        foreach (var slot in _readingSlots)
            slot.BackgroundColor = Profile.Background;
        _pageRail.BackgroundColor = Profile.Surface;
        _outlineList.BackgroundColor = Profile.Surface;
        _sidebar.BackgroundColor = Profile.Surface;
        _titleLabel.TextColor = Profile.Text;
        _statusLabel.TextColor = Profile.Muted;
        foreach (var button in new[]
                 {
                     _openButton,
                     _previousButton,
                     _nextButton,
                     _zoomOutButton,
                     _zoomInButton,
                     _fitButton,
                     _fitWidthButton,
                     _rotateButton,
                 })
        {
            button.BackgroundColor = Profile.Raised;
            button.TextColor = Profile.Text;
            button.BorderColor = Profile.Border;
            button.BorderWidth = GraphicalProfileColors.Stroke;
        }

        ApplyNavButtonTheme();
        StyleNavButton(_fitButton, _fitKind == PdfFitKind.Page);
        StyleNavButton(_fitWidthButton, _fitKind == PdfFitKind.Width);
        _pageEntry.TextColor = Profile.Text;
        _searchBar.TextColor = Profile.Text;
        _searchBar.BackgroundColor = Profile.Raised;
        _searchStatus.TextColor = Profile.Muted;
        _sidebarScrim.Color = Profile.Text.WithAlpha(0.28f);
    }

    private void ApplyNavButtonTheme()
    {
        var pagesOn = !_showOutline && (!IsCompact || _sidebarOpen);
        var outlineOn = _showOutline && (!IsCompact || _sidebarOpen);
        StyleNavButton(_pagesTabButton, pagesOn);
        StyleNavButton(_outlineTabButton, outlineOn);
    }

    private static void StyleNavButton(Button button, bool selected)
    {
        button.BackgroundColor = selected ? Profile.AccentFill : Profile.Raised;
        button.TextColor = selected ? Profile.OnAccentFill : Profile.Text;
        button.BorderColor = Profile.Border;
        button.BorderWidth = GraphicalProfileColors.Stroke;
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
        _openButton.IsEnabled = !_isBusy;
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
        ApplyNavButtonTheme();
        StyleNavButton(_fitButton, _fitKind == PdfFitKind.Page);
        StyleNavButton(_fitWidthButton, _fitKind == PdfFitKind.Width);
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
            FontSize = GraphicalProfileColors.NavigationSize,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Profile.Raised,
            TextColor = Profile.Text,
            BorderColor = Profile.Border,
            BorderWidth = GraphicalProfileColors.Stroke,
            CornerRadius = (int)GraphicalProfileColors.ControlRadius,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
            Padding = new Thickness(12, 8),
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
