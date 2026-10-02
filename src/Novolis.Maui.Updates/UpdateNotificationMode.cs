namespace Novolis.Maui.Updates;

/// <summary>Visual intensity used when a new release candidate is announced.</summary>
public enum UpdateNotificationMode
{
    /// <summary>Shows the update in the host's inline status surface.</summary>
    Inline,
    /// <summary>Shows a transient toast notification.</summary>
    Toast,
    /// <summary>Shows a modal or popup notification.</summary>
    Popup,
}
