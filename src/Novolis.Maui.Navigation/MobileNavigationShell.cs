using Microsoft.Maui.Controls.Shapes;
using Novolis.Commands.Navigation;
using Novolis.Maui.GraphicalProfile;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.Navigation;

/// <summary>
/// Native MAUI mobile shell for a shared semantic menu tree.
/// </summary>
public sealed class MobileNavigationShell : ContentView
{
    readonly MobileNavigationController _navigation;
    readonly Grid _root = new();
    readonly Grid _header = new();
    readonly Grid _primaryBar = new();
    readonly ContentView _body = new();
    readonly Grid _overlayLayer = new();
    readonly Border _overlayCard = new();
    readonly VerticalStackLayout _menuItems = new();
    readonly Label _title = new();
    readonly Label _overlayTitle = new();
    readonly Button _backButton;
    readonly Button _menuButton;
    bool _themeSubscribed;

    /// <summary>Creates a native MAUI shell over a validated navigation controller.</summary>
    public MobileNavigationShell(MobileNavigationController navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        AutomationId = "MobileNavigationShell";
        _backButton = CreateChromeButton("Back", "MobileNavigationBack", Back);
        _menuButton = CreateChromeButton("More", "MobileNavigationMore", OpenMenu);
        Build();
        _navigation.Changed += OnNavigationChanged;
        _navigation.Activated += OnNavigationActivated;
        ApplyTheme();
        Refresh();
    }

    /// <summary>Raised after a route or command node is activated.</summary>
    public event EventHandler<MobileMenuActivationEventArgs>? ActivationRequested;

    /// <summary>Raised after the shared navigation state changes.</summary>
    public event EventHandler<MobileNavigationChangedEventArgs>? NavigationChanged;

    /// <summary>Current shared navigation controller.</summary>
    public MobileNavigationController Controller => _navigation;

    /// <summary>Title shown in the native app bar.</summary>
    public string Title
    {
        get => _title.Text ?? string.Empty;
        set => _title.Text = value ?? string.Empty;
    }

    /// <summary>Route content owned by the host application.</summary>
    public View? Body
    {
        get => _body.Content;
        set => _body.Content = value;
    }

    /// <summary>Applies current Graphical Profile colors to the shell.</summary>
    public void ApplyTheme()
    {
        BackgroundColor = Profile.Background;
        _header.BackgroundColor = Profile.Surface;
        _title.TextColor = Profile.Text;
        _overlayTitle.TextColor = Profile.Text;
        _overlayCard.Background = new SolidColorBrush(Profile.Surface);
        _overlayCard.Stroke = new SolidColorBrush(Profile.Border);
        _menuButton.BackgroundColor = Profile.Raised;
        _menuButton.TextColor = Profile.Text;
        _backButton.BackgroundColor = Profile.Raised;
        _backButton.TextColor = Profile.Text;
        RenderPrimaryBar();
        RenderMenu();
    }

    /// <summary>Lets a platform host forward Android or hardware Back.</summary>
    public bool TryHandleBack() => _navigation.Back();

    /// <summary>Opens the secondary menu surface.</summary>
    public bool OpenSecondaryMenu() => _navigation.OpenSecondaryMenu();

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

    void Build()
    {
        _header.Padding = new Thickness(12, 8, 12, 6);
        _header.ColumnDefinitions =
        [
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Auto),
        ];
        _header.ColumnSpacing = GraphicalProfileColors.ControlGap;
        _header.Add(_backButton, 0, 0);
        _header.Add(_title, 1, 0);
        _header.Add(_menuButton, 2, 0);
        _title.FontFamily = Profile.FontFamily;
        _title.FontSize = GraphicalProfileColors.IntervalTitleSize;
        _title.FontAttributes = FontAttributes.Bold;
        _title.VerticalTextAlignment = TextAlignment.Center;
        _title.LineBreakMode = LineBreakMode.TailTruncation;
        _title.MaxLines = 1;

        _body.HorizontalOptions = LayoutOptions.Fill;
        _body.VerticalOptions = LayoutOptions.Fill;

        _primaryBar.Padding = new Thickness(8, 6);
        _primaryBar.ColumnSpacing = GraphicalProfileColors.ControlGap;
        _primaryBar.MinimumHeightRequest = GraphicalProfileColors.IconControl;

        var overlayHeader = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            ],
            ColumnSpacing = GraphicalProfileColors.ControlGap,
        };
        overlayHeader.Add(_overlayTitle, 0, 0);
        var close = CreateChromeButton(
            "Close",
            "MobileNavigationClose",
            () => _navigation.CloseOverlay());
        overlayHeader.Add(close, 1, 0);
        _overlayTitle.Text = "Menu";
        _overlayTitle.FontFamily = Profile.FontFamily;
        _overlayTitle.FontSize = GraphicalProfileColors.SectionTitleSize;
        _overlayTitle.FontAttributes = FontAttributes.Bold;
        _overlayTitle.VerticalTextAlignment = TextAlignment.Center;
        _menuItems.Spacing = GraphicalProfileColors.ControlGap;
        _overlayCard.Padding = new Thickness(
            GraphicalProfileColors.CardPadding,
            GraphicalProfileColors.CardPadding,
            GraphicalProfileColors.CardPadding,
            GraphicalProfileColors.PageInset);
        _overlayCard.StrokeThickness = GraphicalProfileColors.Stroke;
        _overlayCard.StrokeShape =
            new RoundRectangle { CornerRadius = new CornerRadius(GraphicalProfileColors.CardRadius) };
        _overlayCard.Content = new VerticalStackLayout
        {
            Spacing = GraphicalProfileColors.SectionGap,
            Children =
            {
                overlayHeader,
                _menuItems,
            },
        };
        _overlayCard.HorizontalOptions = LayoutOptions.Fill;
        _overlayCard.VerticalOptions = LayoutOptions.End;

        var scrim = new Button
        {
            AutomationId = "MobileNavigationScrim",
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0,
            Text = string.Empty,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        SemanticProperties.SetDescription(scrim, "Close menu");
        scrim.Clicked += (_, _) => _navigation.CloseOverlay();
        _overlayLayer.Children.Add(scrim);
        _overlayLayer.Children.Add(_overlayCard);
        _overlayLayer.IsVisible = false;

        _root.RowDefinitions =
        [
            new RowDefinition(GridLength.Auto),
            new RowDefinition(GridLength.Star),
            new RowDefinition(GridLength.Auto),
        ];
        _root.Add(_header, 0, 0);
        _root.Add(_body, 0, 1);
        _root.Add(_primaryBar, 0, 2);
        _root.Add(_overlayLayer);
        Grid.SetRowSpan(_overlayLayer, 3);
        Content = _root;
    }

    void Refresh()
    {
        _backButton.IsVisible =
            _navigation.State.Overlay != MobileMenuOverlay.None
            || _navigation.State.RouteStack.Count > 1;
        _menuButton.IsVisible = _navigation.GetSecondaryNodes()
            .Any(node => node.Kind != MobileMenuNodeKind.Separator);
        _overlayLayer.IsVisible = _navigation.State.Overlay != MobileMenuOverlay.None;
        RenderPrimaryBar();
        RenderMenu();
    }

    void RenderPrimaryBar()
    {
        _primaryBar.Children.Clear();
        _primaryBar.ColumnDefinitions.Clear();
        var nodes = _navigation.GetPrimaryNodes();
        _primaryBar.IsVisible = nodes.Count > 0;
        for (var index = 0; index < nodes.Count; index++)
        {
            _primaryBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var node = nodes[index];
            var button = CreateNodeButton(node, $"MobileNavigationPrimary.{node.Id}");
            Grid.SetColumn(button, index);
            _primaryBar.Add(button);
        }
    }

    void RenderMenu()
    {
        _menuItems.Children.Clear();
        if (!_overlayLayer.IsVisible)
            return;

        var current = _navigation.State.CurrentMenuNodeId;
        _overlayTitle.Text = current is null
            ? "Menu"
            : _navigation.Tree.Find(current)?.Label ?? "Menu";
        if (_navigation.State.MenuPath.Count > 0)
        {
            var up = CreateChromeButton(
                "Back",
                "MobileNavigationMenuBack",
                () => _navigation.Back());
            _menuItems.Children.Add(up);
        }

        var nodes = _navigation.GetOpenMenuNodes();
        if (nodes.Count == 0)
        {
            _menuItems.Children.Add(new Label
            {
                Text = "No additional actions",
                FontFamily = Profile.FontFamily,
                FontSize = GraphicalProfileColors.BodySize,
                TextColor = Profile.Muted,
            });
            return;
        }

        foreach (var node in nodes)
        {
            if (node.Kind == MobileMenuNodeKind.Separator)
            {
                _menuItems.Children.Add(new BoxView
                {
                    HeightRequest = GraphicalProfileColors.Stroke,
                    Color = Profile.Border,
                });
                continue;
            }

            _menuItems.Children.Add(
                CreateNodeButton(node, $"MobileNavigationMenu.{node.Id}"));
        }
    }

    Button CreateNodeButton(MobileMenuNode node, string automationId)
    {
        var label = node.Label;
        if (!string.IsNullOrWhiteSpace(node.Subtitle))
            label += $"\n{node.Subtitle}";
        if (node.Kind == MobileMenuNodeKind.Group)
            label += "  ›";
        if (node.Kind == MobileMenuNodeKind.Toggle)
            label += node.IsChecked ? "  On" : "  Off";

        var button = new Button
        {
            AutomationId = automationId,
            Text = label,
            FontFamily = Profile.FontFamily,
            FontSize = GraphicalProfileColors.NavigationSize,
            FontAttributes = FontAttributes.Bold,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
            Padding = new Thickness(12, 7),
            HorizontalOptions = LayoutOptions.Fill,
            IsEnabled = node.IsEnabled,
            BackgroundColor = node.IsSelected ? Profile.AccentFill : Profile.Raised,
            TextColor = node.IsSelected ? Profile.OnAccentFill : Profile.Text,
            BorderColor = Profile.Border,
            BorderWidth = GraphicalProfileColors.Stroke,
            CornerRadius = (int)GraphicalProfileColors.ControlRadius,
        };
        SemanticProperties.SetDescription(
            button,
            node.IsEnabled
                ? node.Label
                : $"{node.Label}. {node.DisabledReason ?? "Unavailable"}");
        button.Clicked += (_, _) => _navigation.Activate(node.Id);
        return button;
    }

    Button CreateChromeButton(string text, string automationId, Func<bool> action)
    {
        var button = new Button
        {
            AutomationId = automationId,
            Text = text,
            FontFamily = Profile.FontFamily,
            FontSize = GraphicalProfileColors.CaptionSize,
            FontAttributes = FontAttributes.Bold,
            MinimumHeightRequest = GraphicalProfileColors.TouchTarget,
            MinimumWidthRequest = GraphicalProfileColors.IconControl,
            Padding = new Thickness(10, 7),
            BackgroundColor = Profile.Raised,
            TextColor = Profile.Text,
            BorderColor = Profile.Border,
            BorderWidth = GraphicalProfileColors.Stroke,
            CornerRadius = (int)GraphicalProfileColors.ControlRadius,
        };
        SemanticProperties.SetDescription(button, text);
        button.Clicked += (_, _) => action();
        return button;
    }

    bool Back() => _navigation.Back();

    bool OpenMenu() => _navigation.OpenSecondaryMenu();

    void OnNavigationChanged(
        object? sender,
        MobileNavigationChangedEventArgs args)
    {
        Refresh();
        NavigationChanged?.Invoke(this, args);
    }

    void OnNavigationActivated(
        object? sender,
        MobileMenuActivationEventArgs args) =>
        ActivationRequested?.Invoke(this, args);

    void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args) =>
        ApplyTheme();
}
