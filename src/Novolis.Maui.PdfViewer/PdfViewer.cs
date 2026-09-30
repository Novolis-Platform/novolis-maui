using System.Security.Cryptography;
using Novolis.Maui.GraphicalProfile;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Documents;
using Novolis.Pdf.Platform;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using Novolis.Pdf.Text;

namespace Novolis.Maui.PdfViewer;

/// <summary>Reusable local PDF page viewer for MAUI Windows and Android hosts.</summary>
public sealed class PdfViewer : ContentView
{
    private const double DefaultDpi = 120;
    private readonly PdfLimits _limits;
    private readonly IPdfDocumentStateStore? _stateStore;
    private readonly PdfSkiaPageRenderer _renderer;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _zoomOutButton;
    private readonly Button _zoomInButton;
    private readonly Button _fitButton;
    private readonly Label _pageLabel;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Image _pageImage;
    private readonly ScrollView _pageScroll;
    private PdfDocumentSession? _session;
    private IReadOnlyList<PdfResolvedPage> _pages = [];
    private byte[]? _currentPageBytes;
    private int _pageIndex;
    private double _zoom = 1;
    private int _rotation;
    private bool _isBusy;

    /// <summary>Creates a viewer with optional local reading-position persistence.</summary>
    public PdfViewer(
        IPdfDocumentStateStore? stateStore = null,
        PdfLimits? limits = null)
    {
        _limits = limits ?? PdfLimits.Default;
        _limits.Validate();
        _stateStore = stateStore;
        _renderer = new PdfSkiaPageRenderer(_limits);

        _previousButton = CreateToolbarButton("Previous", "PdfPreviousPage", GoPreviousAsync);
        _nextButton = CreateToolbarButton("Next", "PdfNextPage", GoNextAsync);
        _zoomOutButton = CreateToolbarButton("Zoom −", "PdfZoomOut", ZoomOutAsync);
        _zoomInButton = CreateToolbarButton("Zoom +", "PdfZoomIn", ZoomInAsync);
        _fitButton = CreateToolbarButton("Fit", "PdfFit", FitAsync);
        _pageLabel = CreateLabel("0 / 0", "PdfPageLabel");
        _titleLabel = CreateLabel("No document open", "PdfDocumentTitle");
        _statusLabel = CreateLabel("Choose a local PDF to begin.", "PdfStatus");
        _pageImage = new Image
        {
            AutomationId = "PdfPageImage",
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(12),
        };
        _pageScroll = new ScrollView
        {
            AutomationId = "PdfPageScroll",
            Orientation = ScrollOrientation.Both,
            Content = _pageImage,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
        };
        _pageScroll.SizeChanged += (_, _) => ApplyImageSize();

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
                _pageLabel,
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
            BackgroundColor = GraphicalProfile.Background,
        };
        layout.Add(header, 0, 0);
        layout.Add(toolbar, 0, 1);
        layout.Add(_pageScroll, 0, 2);
        Content = layout;

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        _pageImage.GestureRecognizers.Add(pinch);
        ApplyTheme();
        UpdateToolbar();
    }

    /// <summary>Raised after the current page changes.</summary>
    public event EventHandler? PageChanged;

    /// <summary>Raised when opening or rendering fails.</summary>
    public event EventHandler<PdfViewerErrorEventArgs>? Error;

    /// <summary>Current reader session, if a document is open.</summary>
    public PdfDocumentSession? Session => _session;

    /// <summary>Current page index, zero-based.</summary>
    public int CurrentPageIndex => _pageIndex;

    /// <summary>Current page number, one-based.</summary>
    public int CurrentPageNumber => _pages.Count == 0 ? 0 : _pageIndex + 1;

    /// <summary>Number of resolved pages.</summary>
    public int PageCount => _pages.Count;

    /// <summary>Current zoom multiplier.</summary>
    public double Zoom => _zoom;

    /// <summary>Current display rotation in degrees.</summary>
    public int Rotation => _rotation;

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
            await using var input = await request.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new MemoryStream();
            await CopyWithLimitAsync(input, buffer, _limits.MaximumDocumentBytes, cancellationToken)
                .ConfigureAwait(false);
            buffer.Position = 0;
            var stableId = request.Descriptor.StableId
                ?? Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
            buffer.Position = 0;
            var source = PdfSources.FromStream(buffer, request.Descriptor.DisplayName, stableId);
            _session = await PdfDocumentSession.OpenAsync(source, _limits, cancellationToken)
                .ConfigureAwait(false);
            _pages = PdfPageTree.Resolve(_session.Document, _limits);
            _pageIndex = 0;
            _zoom = 1;
            _rotation = 0;
            if (_stateStore is not null)
            {
                await _stateStore.RememberAsync(
                        new PdfRecentDocument(
                            stableId,
                            request.Descriptor.DisplayName,
                            request.Descriptor.StableId ?? request.Descriptor.DisplayName,
                            DateTimeOffset.UtcNow),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (await _stateStore.LoadPositionAsync(stableId, cancellationToken).ConfigureAwait(false)
                    is { } position)
                {
                    _pageIndex = Math.Clamp(position.PageIndex, 0, Math.Max(0, _pages.Count - 1));
                    _zoom = Math.Clamp(position.Zoom, 0.5, 4);
                    _rotation = NormalizeRotation(position.Rotation);
                }
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _titleLabel.Text = _session.Document.Info.Title
                    ?? _session.Document.Info.Source.DisplayName;
                ApplyTheme();
                await RenderCurrentPageAsync(cancellationToken);
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
        _currentPageBytes = null;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _pageImage.Source = null;
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
        return RenderCurrentPageAsync(cancellationToken);
    }

    /// <summary>Moves to the next page.</summary>
    public Task GoNextAsync() => GoToPageAsync(_pageIndex + 1);

    /// <summary>Moves to the previous page.</summary>
    public Task GoPreviousAsync() => GoToPageAsync(_pageIndex - 1);

    /// <summary>Increases the page scale.</summary>
    public Task ZoomInAsync() => SetZoomAsync(_zoom + 0.25);

    /// <summary>Decreases the page scale.</summary>
    public Task ZoomOutAsync() => SetZoomAsync(_zoom - 0.25);

    /// <summary>Fits the page to the available viewer width.</summary>
    public Task FitAsync() => SetZoomAsync(1);

    /// <summary>Sets a bounded page scale.</summary>
    public async Task SetZoomAsync(double zoom, CancellationToken cancellationToken = default)
    {
        _zoom = Math.Clamp(zoom, 0.5, 4);
        ApplyImageSize();
        await SavePositionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Rotates the page clockwise by 90 degrees.</summary>
    public async Task RotateAsync(CancellationToken cancellationToken = default)
    {
        _rotation = NormalizeRotation(_rotation + 90);
        await RenderCurrentPageAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RenderCurrentPageAsync(CancellationToken cancellationToken)
    {
        if (_session is null || _pages.Count == 0 || _isBusy)
            return;

        SetBusy(true, $"Rendering page {_pageIndex + 1} of {_pages.Count}…");
        try
        {
            var request = new PdfRenderRequest(_pageIndex, DefaultDpi);
            var rendered = await _renderer.RenderAsync(
                    _session.Document,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);
            _currentPageBytes = rendered.PngBytes;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _pageImage.Source = ImageSource.FromStream(() =>
                    new MemoryStream(_currentPageBytes, writable: false));
                ApplyImageSize();
                SetStatus(rendered.Status == PdfRenderStatus.Rendered
                    ? $"Page {_pageIndex + 1} of {_pages.Count}"
                    : $"Page {_pageIndex + 1} of {_pages.Count} · rendered with warnings");
                UpdateToolbar();
                PageChanged?.Invoke(this, EventArgs.Empty);
            });
            await SavePositionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or PdfReaderException)
        {
            SetStatus("This page could not be rendered.");
            Error?.Invoke(this, new PdfViewerErrorEventArgs(exception));
        }
        finally
        {
            SetBusy(false);
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
        if (args.Status is GestureStatus.Running)
            _ = SetZoomAsync(args.Scale * _zoom);
    }

    private void ApplyImageSize()
    {
        if (_currentPageBytes is null || _pages.Count == 0)
            return;
        var page = _pages[_pageIndex].Info;
        var availableWidth = Math.Max(160, _pageScroll.Width - 28);
        var width = Math.Max(160, availableWidth * _zoom);
        var aspect = page.DisplayHeight <= 0 ? 1 : page.DisplayWidth / page.DisplayHeight;
        _pageImage.WidthRequest = width;
        _pageImage.HeightRequest = width / Math.Max(0.1, aspect);
    }

    private void ApplyTheme()
    {
        BackgroundColor = GraphicalProfile.Background;
        _titleLabel.TextColor = GraphicalProfile.Text;
        _statusLabel.TextColor = GraphicalProfile.Muted;
        foreach (var button in new[] { _previousButton, _nextButton, _zoomOutButton, _zoomInButton, _fitButton })
        {
            button.BackgroundColor = GraphicalProfile.Action;
            button.TextColor = GraphicalProfile.OnAction;
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _isBusy = busy;
        if (status is not null)
            SetStatus(status);
        UpdateToolbar();
    }

    private void SetStatus(string status) => _statusLabel.Text = status;

    private void UpdateToolbar()
    {
        var enabled = !_isBusy && _pages.Count > 0;
        _previousButton.IsEnabled = enabled && _pageIndex > 0;
        _nextButton.IsEnabled = enabled && _pageIndex < _pages.Count - 1;
        _zoomOutButton.IsEnabled = enabled && _zoom > 0.5;
        _zoomInButton.IsEnabled = enabled && _zoom < 4;
        _fitButton.IsEnabled = enabled;
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
