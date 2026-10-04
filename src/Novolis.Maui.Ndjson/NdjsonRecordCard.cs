using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using Novolis.Maui.GraphicalProfile;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.Ndjson;

/// <summary>Virtualized card for one NDJSON record.</summary>
public sealed class NdjsonRecordCard : ContentView
{
    private readonly Border _card;
    private readonly Label _number;
    private readonly Label _status;
    private readonly Label _metadata;
    private readonly Label _preview;
    private readonly ScrollView _detailsScroller;
    private readonly Label _details;
    private readonly Button _expandButton;
    private readonly Button _copyButton;
    private bool _expanded;
    private bool _themeSubscribed;

    /// <summary>Identifies the record displayed by the card.</summary>
    public static readonly BindableProperty RecordProperty =
        BindableProperty.Create(
            nameof(Record),
            typeof(NdjsonRecordDisplay),
            typeof(NdjsonRecordCard),
            default(NdjsonRecordDisplay),
            propertyChanged: static (bindable, _, _) =>
                ((NdjsonRecordCard)bindable).RenderRecord());

    /// <summary>Creates a record card.</summary>
    public NdjsonRecordCard()
    {
        AutomationId = "NdjsonRecordCard";
        _number = new Label
        {
            FontFamily = Profile.MonoFontFamily,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            VerticalTextAlignment = TextAlignment.Center,
        };
        _status = new Label
        {
            FontAttributes = FontAttributes.Bold,
            FontSize = 11,
            VerticalTextAlignment = TextAlignment.Center,
        };
        _metadata = new Label
        {
            FontFamily = Profile.MonoFontFamily,
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center,
        };
        _preview = new Label
        {
            FontFamily = Profile.MonoFontFamily,
            FontSize = 12,
            LineBreakMode = LineBreakMode.NoWrap,
            Padding = new Thickness(2, 0),
        };
        _details = new Label
        {
            FontFamily = Profile.MonoFontFamily,
            FontSize = 12,
            LineBreakMode = LineBreakMode.NoWrap,
            Padding = new Thickness(10, 8),
        };
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
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
                Content = _details,
            },
        };
        _expandButton = CreateButton("Expand", "NdjsonExpand");
        _expandButton.Clicked += OnExpandClicked;
        _copyButton = CreateButton("Copy", "NdjsonCopy");
        _copyButton.Clicked += OnCopyClicked;

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        header.Add(_number, 0, 0);
        header.Add(_status, 1, 0);
        header.Add(_metadata, 2, 0);
        header.Add(_expandButton, 3, 0);
        header.Add(_copyButton, 4, 0);

        var previewScroller = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = _preview,
        };
        var content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                header,
                previewScroller,
                _detailsScroller,
            },
        };
        _card = new Border
        {
            Padding = new Thickness(14, 12),
            StrokeThickness = GraphicalProfileColors.Stroke,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            Content = content,
        };
        Content = _card;
        ApplyTheme();
    }

    /// <summary>Gets or sets the record displayed by this card.</summary>
    public NdjsonRecordDisplay? Record
    {
        get => (NdjsonRecordDisplay?)GetValue(RecordProperty);
        set => SetValue(RecordProperty, value);
    }

    /// <summary>Applies the current graphical-profile colors.</summary>
    public void ApplyTheme()
    {
        _card.Background = new SolidColorBrush(Profile.Raised);
        _card.Stroke = new SolidColorBrush(Profile.Border);
        _number.TextColor = Profile.Text;
        _metadata.TextColor = Profile.Muted;
        _preview.TextColor = Profile.Text;
        _details.TextColor = Profile.Text;
        _expandButton.BackgroundColor = Profile.ActionSoft;
        _expandButton.TextColor = Profile.Text;
        _copyButton.BackgroundColor = Profile.ActionSoft;
        _copyButton.TextColor = Profile.Text;
        if (Record is { } record)
            _status.TextColor = record.IsValid ? Profile.Accent : Profile.Danger;
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

    private void RenderRecord()
    {
        var record = Record;
        if (record is null)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
        _expanded = false;
        _number.Text = $"#{record.Number:N0}";
        _status.Text = record.Status;
        _metadata.Text = $"{record.ByteLength:N0} B · offset {record.ByteOffset:N0}";
        _preview.Text = record.Preview;
        _details.Text = string.Empty;
        _detailsScroller.IsVisible = false;
        _expandButton.Text = "Expand";
        _expandButton.AutomationId = $"NdjsonExpand{record.Number}";
        _copyButton.AutomationId = $"NdjsonCopy{record.Number}";
        SemanticProperties.SetDescription(this, $"NDJSON record {record.Number}");
        ApplyTheme();
    }

    private void OnExpandClicked(object? sender, EventArgs args)
    {
        if (Record is not { } record)
            return;

        _expanded = !_expanded;
        if (_expanded)
            _details.Text = record.Details;
        _detailsScroller.IsVisible = _expanded;
        _expandButton.Text = _expanded ? "Collapse" : "Expand";
    }

    private async void OnCopyClicked(object? sender, EventArgs args)
    {
        if (Record is not { } record)
            return;

        await Clipboard.Default.SetTextAsync(record.CopyText);
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args) =>
        ApplyTheme();

    private static Button CreateButton(string text, string automationId) =>
        new()
        {
            Text = text,
            AutomationId = automationId,
            FontFamily = Profile.FontFamily,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
            Padding = new Thickness(11, 6),
            CornerRadius = (int)GraphicalProfileColors.ControlRadius,
            BorderWidth = GraphicalProfileColors.Stroke,
            BorderColor = Profile.Border,
        };
}
