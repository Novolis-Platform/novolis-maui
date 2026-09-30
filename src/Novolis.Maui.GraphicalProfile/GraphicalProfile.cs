using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

#pragma warning disable CS1591

namespace Novolis.Maui.GraphicalProfile;

/// <summary>
/// Installs the required Novolis graphical profile into a MAUI application.
/// </summary>
public static class GraphicalProfile
{
    public const string BackgroundResourceKey = "Ngp.Background";
    public const string SurfaceResourceKey = "Ngp.Surface";
    public const string RaisedResourceKey = "Ngp.Raised";
    public const string BorderResourceKey = "Ngp.Border";
    public const string TextResourceKey = "Ngp.Text";
    public const string MutedResourceKey = "Ngp.Muted";
    public const string AccentResourceKey = "Ngp.Accent";
    public const string AccentFillResourceKey = "Ngp.AccentFill";
    public const string OnAccentFillResourceKey = "Ngp.OnAccentFill";
    public const string ActionResourceKey = "Ngp.Action";
    public const string OnActionResourceKey = "Ngp.OnAction";
    public const string ActionSoftResourceKey = "Ngp.ActionSoft";
    public const string WarningResourceKey = "Ngp.Warning";
    public const string DangerResourceKey = "Ngp.Danger";

    public const string FontFamily = GraphicalProfileColors.FontFamily;
    public const string MonoFontFamily = GraphicalProfileColors.MonoFontFamily;

    public static Color Background => CurrentColor(
        BackgroundResourceKey,
        GraphicalProfileColors.BackgroundDark);

    public static Color Surface => CurrentColor(
        SurfaceResourceKey,
        GraphicalProfileColors.SurfaceDark);

    public static Color Raised => CurrentColor(
        RaisedResourceKey,
        GraphicalProfileColors.RaisedDark);

    public static Color Border => CurrentColor(
        BorderResourceKey,
        GraphicalProfileColors.BorderDark);

    public static Color Text => CurrentColor(
        TextResourceKey,
        GraphicalProfileColors.TextDark);

    public static Color Muted => CurrentColor(
        MutedResourceKey,
        GraphicalProfileColors.MutedDark);

    public static Color Accent => CurrentColor(
        AccentResourceKey,
        GraphicalProfileColors.AccentDark);

    public static Color AccentFill => CurrentColor(
        AccentFillResourceKey,
        GraphicalProfileColors.AccentFillDark);

    public static Color OnAccentFill => CurrentColor(
        OnAccentFillResourceKey,
        GraphicalProfileColors.OnAccentFillDark);

    public static Color Action => CurrentColor(
        ActionResourceKey,
        GraphicalProfileColors.ActionDark);

    public static Color OnAction => CurrentColor(
        OnActionResourceKey,
        GraphicalProfileColors.OnActionDark);

    public static Color ActionSoft => CurrentColor(
        ActionSoftResourceKey,
        GraphicalProfileColors.ActionSoftDark);

    public static Color Warning => CurrentColor(
        WarningResourceKey,
        GraphicalProfileColors.WarningDark);

    public static Color Danger => CurrentColor(
        DangerResourceKey,
        GraphicalProfileColors.DangerDark);

    /// <summary>Installs dynamic resources and theme-change propagation.</summary>
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

    private static void OnRequestedThemeChanged(
        object? sender,
        AppThemeChangedEventArgs args)
    {
        if (sender is Application application)
        {
            ApplyResources(application, args.RequestedTheme);
        }
    }

    private static void ApplyResources(Application application, AppTheme theme)
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

    private static void AddColor(
        Application application,
        string key,
        string value) =>
        application.Resources[key] = Color.FromArgb(value);

    private static Color CurrentColor(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }

        return Color.FromArgb(fallback);
    }
}
