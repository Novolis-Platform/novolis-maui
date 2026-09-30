using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Novolis.Maui.GraphicalProfile;

/// <summary>
/// Installs the required Novolis graphical profile into a MAUI application.
/// </summary>
public static class GraphicalProfile
{
    /// <summary>Dynamic resource key for the page canvas.</summary>
    public const string BackgroundResourceKey = "Ngp.Background";

    /// <summary>Dynamic resource key for cards and fields.</summary>
    public const string SurfaceResourceKey = "Ngp.Surface";

    /// <summary>Dynamic resource key for selected and raised rows.</summary>
    public const string RaisedResourceKey = "Ngp.Raised";

    /// <summary>Dynamic resource key for structural strokes.</summary>
    public const string BorderResourceKey = "Ngp.Border";

    /// <summary>Dynamic resource key for primary text.</summary>
    public const string TextResourceKey = "Ngp.Text";

    /// <summary>Dynamic resource key for supporting text.</summary>
    public const string MutedResourceKey = "Ngp.Muted";

    /// <summary>Dynamic resource key for eyebrows and focus.</summary>
    public const string AccentResourceKey = "Ngp.Accent";

    /// <summary>Dynamic resource key for open and navigate actions.</summary>
    public const string AccentFillResourceKey = "Ngp.AccentFill";

    /// <summary>Dynamic resource key for text on accent fill.</summary>
    public const string OnAccentFillResourceKey = "Ngp.OnAccentFill";

    /// <summary>Dynamic resource key for the one commit action.</summary>
    public const string ActionResourceKey = "Ngp.Action";

    /// <summary>Dynamic resource key for text on the commit action.</summary>
    public const string OnActionResourceKey = "Ngp.OnAction";

    /// <summary>Dynamic resource key for informational chips.</summary>
    public const string ActionSoftResourceKey = "Ngp.ActionSoft";

    /// <summary>Dynamic resource key for validation copy.</summary>
    public const string WarningResourceKey = "Ngp.Warning";

    /// <summary>Dynamic resource key for failure copy.</summary>
    public const string DangerResourceKey = "Ngp.Danger";

    /// <summary>Body face from the governance bundle.</summary>
    public const string FontFamily = GraphicalProfileColors.FontFamily;

    /// <summary>Monospace face from the governance bundle.</summary>
    public const string MonoFontFamily = GraphicalProfileColors.MonoFontFamily;

    /// <summary>Current background role.</summary>
    public static Color Background => CurrentColor(
        BackgroundResourceKey,
        GraphicalProfileColors.BackgroundDark);

    /// <summary>Current surface role.</summary>
    public static Color Surface => CurrentColor(
        SurfaceResourceKey,
        GraphicalProfileColors.SurfaceDark);

    /// <summary>Current raised role.</summary>
    public static Color Raised => CurrentColor(
        RaisedResourceKey,
        GraphicalProfileColors.RaisedDark);

    /// <summary>Current border role.</summary>
    public static Color Border => CurrentColor(
        BorderResourceKey,
        GraphicalProfileColors.BorderDark);

    /// <summary>Current text role.</summary>
    public static Color Text => CurrentColor(
        TextResourceKey,
        GraphicalProfileColors.TextDark);

    /// <summary>Current muted role.</summary>
    public static Color Muted => CurrentColor(
        MutedResourceKey,
        GraphicalProfileColors.MutedDark);

    /// <summary>Current accent role.</summary>
    public static Color Accent => CurrentColor(
        AccentResourceKey,
        GraphicalProfileColors.AccentDark);

    /// <summary>Current accent fill role.</summary>
    public static Color AccentFill => CurrentColor(
        AccentFillResourceKey,
        GraphicalProfileColors.AccentFillDark);

    /// <summary>Current on-accent-fill role.</summary>
    public static Color OnAccentFill => CurrentColor(
        OnAccentFillResourceKey,
        GraphicalProfileColors.OnAccentFillDark);

    /// <summary>Current action role.</summary>
    public static Color Action => CurrentColor(
        ActionResourceKey,
        GraphicalProfileColors.ActionDark);

    /// <summary>Current on-action role.</summary>
    public static Color OnAction => CurrentColor(
        OnActionResourceKey,
        GraphicalProfileColors.OnActionDark);

    /// <summary>Current action-soft role.</summary>
    public static Color ActionSoft => CurrentColor(
        ActionSoftResourceKey,
        GraphicalProfileColors.ActionSoftDark);

    /// <summary>Current warning role.</summary>
    public static Color Warning => CurrentColor(
        WarningResourceKey,
        GraphicalProfileColors.WarningDark);

    /// <summary>Current danger role.</summary>
    public static Color Danger => CurrentColor(
        DangerResourceKey,
        GraphicalProfileColors.DangerDark);

    /// <summary>Installs <c>Ngp.*</c> keys and keeps them aligned with the requested theme.</summary>
    public static void Install(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (application.Resources.TryGetValue("Ngp.Installed", out _))
        {
            return;
        }

        ApplyResources(application, application.RequestedTheme);
        application.RequestedThemeChanged += OnRequestedThemeChanged;
        application.Resources["Ngp.Installed"] = true;
    }

    static void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        if (sender is Application application)
        {
            ApplyResources(application, args.RequestedTheme);
        }
    }

    static void ApplyResources(Application application, AppTheme theme)
    {
        var light = theme == AppTheme.Light;
        AddColor(application, BackgroundResourceKey, light
            ? GraphicalProfileColors.BackgroundLight
            : GraphicalProfileColors.BackgroundDark);
        AddColor(application, SurfaceResourceKey, light
            ? GraphicalProfileColors.SurfaceLight
            : GraphicalProfileColors.SurfaceDark);
        AddColor(application, RaisedResourceKey, light
            ? GraphicalProfileColors.RaisedLight
            : GraphicalProfileColors.RaisedDark);
        AddColor(application, BorderResourceKey, light
            ? GraphicalProfileColors.BorderLight
            : GraphicalProfileColors.BorderDark);
        AddColor(application, TextResourceKey, light
            ? GraphicalProfileColors.TextLight
            : GraphicalProfileColors.TextDark);
        AddColor(application, MutedResourceKey, light
            ? GraphicalProfileColors.MutedLight
            : GraphicalProfileColors.MutedDark);
        AddColor(application, AccentResourceKey, light
            ? GraphicalProfileColors.AccentLight
            : GraphicalProfileColors.AccentDark);
        AddColor(application, AccentFillResourceKey, light
            ? GraphicalProfileColors.AccentFillLight
            : GraphicalProfileColors.AccentFillDark);
        AddColor(application, OnAccentFillResourceKey, light
            ? GraphicalProfileColors.OnAccentFillLight
            : GraphicalProfileColors.OnAccentFillDark);
        AddColor(application, ActionResourceKey, light
            ? GraphicalProfileColors.ActionLight
            : GraphicalProfileColors.ActionDark);
        AddColor(application, OnActionResourceKey, light
            ? GraphicalProfileColors.OnActionLight
            : GraphicalProfileColors.OnActionDark);
        AddColor(application, ActionSoftResourceKey, light
            ? GraphicalProfileColors.ActionSoftLight
            : GraphicalProfileColors.ActionSoftDark);
        AddColor(application, WarningResourceKey, light
            ? GraphicalProfileColors.WarningLight
            : GraphicalProfileColors.WarningDark);
        AddColor(application, DangerResourceKey, light
            ? GraphicalProfileColors.DangerLight
            : GraphicalProfileColors.DangerDark);
    }

    static void AddColor(Application application, string key, string value) =>
        application.Resources[key] = Color.FromArgb(value);

    static Color CurrentColor(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }

        return Color.FromArgb(fallback);
    }
}
