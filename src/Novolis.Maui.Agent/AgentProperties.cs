using Novolis.Maui.Agent.Protocol;

namespace Novolis.Maui.Agent;

/// <summary>Attached agent id/role metadata for MAUI visuals.</summary>
public static class AgentProperties
{
    /// <summary>Identifies a stable agent id.</summary>
    public static readonly BindableProperty IdProperty =
        BindableProperty.CreateAttached("Id", typeof(string), typeof(AgentProperties), null);

    /// <summary>Identifies a semantic role.</summary>
    public static readonly BindableProperty RoleProperty =
        BindableProperty.CreateAttached("Role", typeof(string), typeof(AgentProperties), null);

    /// <summary>When true, the walker skips this visual.</summary>
    public static readonly BindableProperty IgnoreProperty =
        BindableProperty.CreateAttached("Ignore", typeof(bool), typeof(AgentProperties), false);

    /// <summary>Reads the attached agent id.</summary>
    public static string? GetId(BindableObject visual) => (string?)visual.GetValue(IdProperty);

    /// <summary>Writes the attached agent id.</summary>
    public static void SetId(BindableObject visual, string? value) => visual.SetValue(IdProperty, value);

    /// <summary>Reads the attached role.</summary>
    public static string? GetRole(BindableObject visual) => (string?)visual.GetValue(RoleProperty);

    /// <summary>Writes the attached role.</summary>
    public static void SetRole(BindableObject visual, string? value) => visual.SetValue(RoleProperty, value);

    /// <summary>Reads whether the walker should skip this visual.</summary>
    public static bool GetIgnore(BindableObject visual) => (bool)visual.GetValue(IgnoreProperty);

    /// <summary>Writes whether the walker should skip this visual.</summary>
    public static void SetIgnore(BindableObject visual, bool value) => visual.SetValue(IgnoreProperty, value);

    /// <summary>Sets id and optional role, inferring a role when omitted.</summary>
    public static void SetId(Element visual, string id, string? role = null)
    {
        SetId(visual, id);
        if (role is not null)
            SetRole(visual, role);
        else if (GetRole(visual) is null)
            SetRole(visual, InferRole(visual));
    }

    /// <summary>Infers a protocol role from the MAUI type.</summary>
    public static string InferRole(Element visual) => visual switch
    {
        CheckBox => AgentRoleNames.CheckBox,
        Switch => AgentRoleNames.Toggle,
        Button => AgentRoleNames.Button,
        Entry or Editor or SearchBar => AgentRoleNames.TextBox,
        CollectionView => AgentRoleNames.ListBox,
        Picker => AgentRoleNames.ComboBox,
        TabbedPage => AgentRoleNames.TabControl,
        Page or Window => AgentRoleNames.Window,
        _ => AgentRoleNames.Other
    };
}
