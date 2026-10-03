using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Novolis.IO.Maps;

namespace Novolis.Maui.Map;

/// <summary>
/// Pan-and-zoom MAUI canvas for provider-neutral projected scene points.
/// It deliberately has no terrestrial projection or tile dependency.
/// </summary>
public sealed class ProjectedSceneView : GraphicsView, IDrawable
{
    const double MinimumScale = 0.1;
    const double MaximumScale = 400;

    double _scale = 12;
    double _offsetX;
    double _offsetY;
    double _panStartOffsetX;
    double _panStartOffsetY;
    double _pinchStartScale;
    (double X, double Y) _pinchAnchor;
    bool _pinchActive;
    bool _showLabels = true;

    /// <summary>Creates a projected scene surface.</summary>
    public ProjectedSceneView()
    {
        Drawable = this;
        BackgroundColor = Colors.Black;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        AutomationId = "ProjectedSceneView";
        SemanticProperties.SetDescription(
            this,
            "Interactive projected scene. Pan to move, pinch to zoom, and tap a point to inspect it.");
        SemanticProperties.SetHint(this, "Projected scene");

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(pan);

        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        GestureRecognizers.Add(pinch);

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        GestureRecognizers.Add(tap);

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += OnDoubleTapped;
        GestureRecognizers.Add(doubleTap);
    }

    /// <summary>Points rendered by the scene surface.</summary>
    public IReadOnlyList<ProjectedScenePoint>? Points
    {
        get;
        set
        {
            field = value;
            ReconcileSelection();
            InvalidateScene();
        }
    }

    /// <summary>Currently selected scene point.</summary>
    public ProjectedScenePoint? SelectedPoint { get; private set; }

    /// <summary>Current world-to-screen scale.</summary>
    public double WorldScale => _scale;

    /// <summary>Whether labels are rendered beside points.</summary>
    public bool ShowLabels
    {
        get => _showLabels;
        set
        {
            if (_showLabels == value)
                return;

            _showLabels = value;
            InvalidateScene();
        }
    }

    /// <summary>Minimum screen radius used for point hit testing.</summary>
    public double HitRadiusPixels { get; set; } = 8;

    /// <summary>Background field color.</summary>
    public Color FieldColor { get; set; } = Colors.Black;

    /// <summary>Default scene point color.</summary>
    public Color PointColor { get; set; } = Colors.White;

    /// <summary>Selected scene point color.</summary>
    public Color SelectedPointColor { get; set; } = Colors.Gold;

    /// <summary>Label color.</summary>
    public Color LabelColor { get; set; } = Colors.LightGray;

    /// <summary>Raised when a scene point is selected by the user or host.</summary>
    public event Action<ProjectedScenePoint>? PointSelected;

    /// <summary>Raised when the scene selection changes, including a clear selection.</summary>
    public event Action<ProjectedScenePoint?>? SelectionChanged;

    /// <summary>Selects a point by stable id.</summary>
    public bool SelectPoint(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            ClearSelection();
            return false;
        }

        var point = Points?.FirstOrDefault(
            item => string.Equals(item.Id, id, StringComparison.Ordinal));
        if (point is null)
        {
            ClearSelection();
            return false;
        }

        SetSelection(point, notifyPoint: true);
        return true;
    }

    /// <summary>Clears the selected scene point.</summary>
    public void ClearSelection() => SetSelection(null, notifyPoint: false);

    /// <summary>Centers and scales the camera so every point fits in the viewport.</summary>
    public void FitToPoints(
        double paddingFraction = 0.08,
        double minimumScale = MinimumScale,
        double maximumScale = MaximumScale)
    {
        if (Points is not { Count: > 0 } points || !HasValidSize())
            return;
        if (!double.IsFinite(paddingFraction)
            || paddingFraction is < 0 or >= 0.5)
        {
            throw new ArgumentOutOfRangeException(nameof(paddingFraction));
        }
        if (!double.IsFinite(minimumScale) || minimumScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumScale));
        if (!double.IsFinite(maximumScale) || maximumScale < minimumScale)
            throw new ArgumentOutOfRangeException(nameof(maximumScale));

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        var spanX = global::System.Math.Max(maxX - minX, 1e-6);
        var spanY = global::System.Math.Max(maxY - minY, 1e-6);
        var usableWidth = Width * (1 - 2 * paddingFraction);
        var usableHeight = Height * (1 - 2 * paddingFraction);
        _scale = global::System.Math.Clamp(
            global::System.Math.Min(usableWidth / spanX, usableHeight / spanY),
            minimumScale,
            maximumScale);
        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;
        _offsetX = -centerX * _scale;
        _offsetY = centerY * _scale;
        InvalidateScene();
    }

    /// <summary>Sets an explicit world-center and scale camera.</summary>
    public void SetCamera(double centerX, double centerY, double scale)
    {
        if (!double.IsFinite(centerX))
            throw new ArgumentOutOfRangeException(nameof(centerX));
        if (!double.IsFinite(centerY))
            throw new ArgumentOutOfRangeException(nameof(centerY));
        if (!double.IsFinite(scale))
            throw new ArgumentOutOfRangeException(nameof(scale));

        _scale = global::System.Math.Clamp(scale, MinimumScale, MaximumScale);
        _offsetX = -centerX * _scale;
        _offsetY = centerY * _scale;
        InvalidateScene();
    }

    /// <summary>Pans the scene by a screen-pixel delta.</summary>
    public void PanBy(double deltaX, double deltaY)
    {
        if (!double.IsFinite(deltaX))
            throw new ArgumentOutOfRangeException(nameof(deltaX));
        if (!double.IsFinite(deltaY))
            throw new ArgumentOutOfRangeException(nameof(deltaY));

        _offsetX += deltaX;
        _offsetY += deltaY;
        InvalidateScene();
    }

    /// <summary>Zooms while preserving the source coordinate beneath a screen point.</summary>
    public void ZoomAt(double screenX, double screenY, double scale)
    {
        if (!HasValidSize())
            return;
        if (!double.IsFinite(screenX))
            throw new ArgumentOutOfRangeException(nameof(screenX));
        if (!double.IsFinite(screenY))
            throw new ArgumentOutOfRangeException(nameof(screenY));
        if (!double.IsFinite(scale))
            throw new ArgumentOutOfRangeException(nameof(scale));

        var anchor = ScreenToWorld(screenX, screenY);
        _scale = global::System.Math.Clamp(scale, MinimumScale, MaximumScale);
        _offsetX = screenX - Width / 2 - anchor.X * _scale;
        _offsetY = screenY - Height / 2 + anchor.Y * _scale;
        InvalidateScene();
    }

    /// <summary>Routes a platform-hosted screen tap through point hit testing and selection.</summary>
    public bool HandleScreenTap(double screenX, double screenY)
    {
        if (!HasValidSize())
            return false;

        var hit = HitTest(screenX, screenY);
        if (hit is null)
        {
            ClearSelection();
            return false;
        }

        SetSelection(hit, notifyPoint: true);
        return true;
    }

    /// <inheritdoc />
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = FieldColor;
        canvas.FillRectangle(dirtyRect);
        if (!HasValidSize() || Points is not { Count: > 0 } points)
            return;

        foreach (var point in points)
        {
            var screen = WorldToScreen(point.X, point.Y);
            var selected = string.Equals(
                point.Id,
                SelectedPoint?.Id,
                StringComparison.Ordinal);
            var radius = point.RadiusPixels ?? (selected ? 5 : 3);
            if (screen.X < -radius
                || screen.X > Width + radius
                || screen.Y < -radius
                || screen.Y > Height + radius)
            {
                continue;
            }

            canvas.FillColor = selected ? SelectedPointColor : PointColor;
            canvas.FillCircle((float)screen.X, (float)screen.Y, (float)radius);

            if (selected)
            {
                canvas.StrokeColor = SelectedPointColor;
                canvas.StrokeSize = 2;
                canvas.DrawCircle((float)screen.X, (float)screen.Y, (float)(radius + 3));
            }

            if (ShowLabels && !string.IsNullOrWhiteSpace(point.Label))
            {
                canvas.FontColor = LabelColor;
                canvas.FontSize = 11;
                canvas.DrawString(
                    point.Label,
                    (float)(screen.X + radius + 4),
                    (float)(screen.Y - 10),
                    220,
                    20,
                    HorizontalAlignment.Left,
                    VerticalAlignment.Center);
            }
        }
    }

    void OnPanUpdated(object? sender, PanUpdatedEventArgs args)
    {
        if (_pinchActive)
            return;

        switch (args.StatusType)
        {
            case GestureStatus.Started:
                _panStartOffsetX = _offsetX;
                _panStartOffsetY = _offsetY;
                break;
            case GestureStatus.Running:
                _offsetX = _panStartOffsetX + args.TotalX;
                _offsetY = _panStartOffsetY + args.TotalY;
                InvalidateScene();
                break;
        }
    }

    void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs args)
    {
        if (!HasValidSize())
            return;

        switch (args.Status)
        {
            case GestureStatus.Started:
                _pinchActive = true;
                _pinchStartScale = _scale;
                _pinchAnchor = ScreenToWorld(
                    args.ScaleOrigin.X * Width,
                    args.ScaleOrigin.Y * Height);
                break;
            case GestureStatus.Running:
                var screenX = args.ScaleOrigin.X * Width;
                var screenY = args.ScaleOrigin.Y * Height;
                _scale = global::System.Math.Clamp(
                    _pinchStartScale * global::System.Math.Max(args.Scale, 0.01),
                    MinimumScale,
                    MaximumScale);
                _offsetX = screenX - Width / 2 - _pinchAnchor.X * _scale;
                _offsetY = screenY - Height / 2 + _pinchAnchor.Y * _scale;
                InvalidateScene();
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _pinchActive = false;
                break;
        }
    }

    void OnTapped(object? sender, TappedEventArgs args)
    {
        var position = args.GetPosition(this);
        if (position is { } point)
            HandleScreenTap(point.X, point.Y);
    }

    void OnDoubleTapped(object? sender, TappedEventArgs args)
    {
        var position = args.GetPosition(this);
        if (position is { } point)
            ZoomAt(point.X, point.Y, _scale * 1.5);
    }

    ProjectedScenePoint? HitTest(double screenX, double screenY)
    {
        if (Points is not { Count: > 0 } points)
            return null;

        ProjectedScenePoint? nearest = null;
        var nearestDistanceSquared = double.PositiveInfinity;
        foreach (var point in points)
        {
            var screen = WorldToScreen(point.X, point.Y);
            var radius = global::System.Math.Max(
                HitRadiusPixels,
                point.RadiusPixels.GetValueOrDefault(3) + 4);
            var deltaX = screen.X - screenX;
            var deltaY = screen.Y - screenY;
            var distanceSquared = deltaX * deltaX + deltaY * deltaY;
            if (distanceSquared > radius * radius
                || distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearest = point;
            nearestDistanceSquared = distanceSquared;
        }

        return nearest;
    }

    (double X, double Y) ScreenToWorld(double screenX, double screenY) =>
        (
            (screenX - Width / 2 - _offsetX) / _scale,
            (Height / 2 - screenY + _offsetY) / _scale);

    (double X, double Y) WorldToScreen(double worldX, double worldY) =>
        (
            Width / 2 + worldX * _scale + _offsetX,
            Height / 2 - worldY * _scale + _offsetY);

    void ReconcileSelection()
    {
        if (SelectedPoint is not { } selected)
            return;

        var replacement = Points?.FirstOrDefault(
            point => string.Equals(point.Id, selected.Id, StringComparison.Ordinal));
        SetSelection(replacement, notifyPoint: false);
    }

    void SetSelection(ProjectedScenePoint? point, bool notifyPoint)
    {
        if (ReferenceEquals(SelectedPoint, point))
            return;

        var changed = !string.Equals(
            SelectedPoint?.Id,
            point?.Id,
            StringComparison.Ordinal);
        SelectedPoint = point;
        InvalidateScene();
        if (changed)
            SelectionChanged?.Invoke(point);
        if (point is not null && notifyPoint)
            PointSelected?.Invoke(point);
    }

    void InvalidateScene() => Invalidate();

    bool HasValidSize() =>
        double.IsFinite(Width)
        && double.IsFinite(Height)
        && Width > 0
        && Height > 0;
}
