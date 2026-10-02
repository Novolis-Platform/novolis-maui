using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Novolis.Maui.GraphicalProfile;
using Novolis.Registry.Primitives.Updates;
using Novolis.Registry.Updates;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;

namespace Novolis.Maui.Updates;

/// <summary>
/// Profile-bound MAUI update surface. Installation and notification behavior
/// remains in the product host through <see cref="IUpdateHostActions"/>.
/// </summary>
public sealed class UpdateStatusView : ContentView
{
    public static readonly BindableProperty CoordinatorProperty =
        BindableProperty.Create(
            nameof(Coordinator),
            typeof(UpdateCoordinator),
            typeof(UpdateStatusView),
            null,
            propertyChanged: OnCoordinatorChanged);

    public static readonly BindableProperty HostActionsProperty =
        BindableProperty.Create(
            nameof(HostActions),
            typeof(IUpdateHostActions),
            typeof(UpdateStatusView),
            null);

    public static readonly BindableProperty NotificationModeProperty =
        BindableProperty.Create(
            nameof(NotificationMode),
            typeof(UpdateNotificationMode),
            typeof(UpdateStatusView),
            UpdateNotificationMode.Inline);

    public static readonly BindableProperty ShowInlineProperty =
        BindableProperty.Create(
            nameof(ShowInline),
            typeof(bool),
            typeof(UpdateStatusView),
            true,
            propertyChanged: OnShowInlineChanged);

    private readonly Label _status = CreateLabel("Status", "UpdateStatusView.StatusText");
    private readonly Label _currentVersion = CreateLabel("Current version", "UpdateStatusView.CurrentVersion");
    private readonly Label _candidateVersion = CreateLabel("No update available", "UpdateStatusView.CandidateVersion");
    private readonly Label _releaseNotes = CreateLabel("", "UpdateStatusView.ReleaseNotes");
    private readonly Label _error = CreateLabel("", "UpdateStatusView.ErrorText");
    private readonly ProgressBar _progress = new() { Progress = 0, IsVisible = false };
    private readonly Button _checkButton = CreateButton("Check for updates", "UpdateStatusView.CheckButton");
    private readonly Button _releaseButton = CreateButton("View release", "UpdateStatusView.ReleaseButton");
    private readonly Button _downloadButton = CreateButton("Download", "UpdateStatusView.DownloadButton");
    private readonly Button _snoozeButton = CreateButton("Later", "UpdateStatusView.SnoozeButton");
    private readonly Button _revealButton = CreateButton("Show downloaded file", "UpdateStatusView.RevealButton");
    private readonly Button _applyButton = CreateButton("Continue installation", "UpdateStatusView.ApplyButton");
    private int _notificationInFlight;
    private UpdateCoordinator? _subscribedCoordinator;

    /// <summary>Creates the profile-bound update surface.</summary>
    public UpdateStatusView()
    {
        AutomationId = "UpdateStatusView";
        SemanticProperties.SetDescription(this, "Application updates");
        _releaseNotes.LineBreakMode = LineBreakMode.WordWrap;
        _error.LineBreakMode = LineBreakMode.WordWrap;
        _error.TextColor = Colors.Red;

        _checkButton.Clicked += async (_, _) => await CheckAsync().ConfigureAwait(false);
        _releaseButton.Clicked += async (_, _) => await OpenReleaseAsync().ConfigureAwait(false);
        _downloadButton.Clicked += async (_, _) => await DownloadAsync().ConfigureAwait(false);
        _snoozeButton.Clicked += async (_, _) => await SnoozeAsync().ConfigureAwait(false);
        _revealButton.Clicked += async (_, _) => await RevealAsync().ConfigureAwait(false);
        _applyButton.Clicked += async (_, _) => await ApplyAsync().ConfigureAwait(false);

        var actionRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _checkButton,
                _releaseButton,
                _downloadButton,
                _snoozeButton,
                _revealButton,
                _applyButton,
            },
        };
        var content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _candidateVersion,
                _status,
                _currentVersion,
                _releaseNotes,
                _progress,
                _error,
                actionRow,
            },
        };
        var card = new Border
        {
            Padding = new Thickness(16),
            Content = content,
            StrokeThickness = 1,
        };
        card.SetDynamicResource(VisualElement.BackgroundColorProperty, Profile.SurfaceResourceKey);
        card.SetDynamicResource(Border.StrokeProperty, Profile.BorderResourceKey);
        Content = card;
        IsVisible = ShowInline;
    }

    /// <summary>Coordinator supplying the neutral update snapshot.</summary>
    public UpdateCoordinator? Coordinator
    {
        get => (UpdateCoordinator?)GetValue(CoordinatorProperty);
        set => SetValue(CoordinatorProperty, value);
    }

    /// <summary>Host callbacks for notification and platform actions.</summary>
    public IUpdateHostActions? HostActions
    {
        get => (IUpdateHostActions?)GetValue(HostActionsProperty);
        set => SetValue(HostActionsProperty, value);
    }

    /// <summary>Notification surface used for a newly discovered candidate.</summary>
    public UpdateNotificationMode NotificationMode
    {
        get => (UpdateNotificationMode)GetValue(NotificationModeProperty);
        set => SetValue(NotificationModeProperty, value);
    }

    /// <summary>Whether the inline status card is visible.</summary>
    public bool ShowInline
    {
        get => (bool)GetValue(ShowInlineProperty);
        set => SetValue(ShowInlineProperty, value);
    }

    /// <summary>Reapplies graphical-profile dynamic resources after host theme changes.</summary>
    public void ReapplyProfileResources()
    {
        if (Content is Border card)
        {
            card.SetDynamicResource(VisualElement.BackgroundColorProperty, Profile.SurfaceResourceKey);
            card.SetDynamicResource(Border.StrokeProperty, Profile.BorderResourceKey);
        }
    }

    private static void OnCoordinatorChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var view = (UpdateStatusView)bindable;
        view.Unsubscribe(oldValue as UpdateCoordinator);
        var coordinator = newValue as UpdateCoordinator;
        view.Subscribe(coordinator);
        view.Render(coordinator?.Snapshot ?? new UpdateSnapshot { CurrentVersion = "unknown" });
    }

    private static void OnShowInlineChanged(BindableObject bindable, object? _, object? newValue) =>
        ((UpdateStatusView)bindable).IsVisible = newValue is true;

    private void Subscribe(UpdateCoordinator? coordinator)
    {
        if (coordinator is null)
            return;
        _subscribedCoordinator = coordinator;
        coordinator.SnapshotChanged += OnSnapshotChanged;
        Render(coordinator.Snapshot);
    }

    private void Unsubscribe(UpdateCoordinator? coordinator)
    {
        if (coordinator is null || !ReferenceEquals(_subscribedCoordinator, coordinator))
            return;
        coordinator.SnapshotChanged -= OnSnapshotChanged;
        _subscribedCoordinator = null;
    }

    private void OnSnapshotChanged(UpdateSnapshot snapshot) =>
        MainThread.BeginInvokeOnMainThread(() => Render(snapshot));

    private void Render(UpdateSnapshot snapshot)
    {
        _status.Text = snapshot.State switch
        {
            UpdateState.Available => "An update is available",
            UpdateState.Downloading => "Downloading update…",
            UpdateState.DownloadReady => "Download ready",
            UpdateState.Deferred => "Update deferred",
            UpdateState.UpToDate => "You are up to date",
            UpdateState.Failed => snapshot.ErrorMessage ?? "Update check failed",
            UpdateState.Checking => "Checking for updates…",
            _ => "Updates",
        };
        _currentVersion.Text = $"Current version: {snapshot.CurrentVersion}";
        _candidateVersion.Text = snapshot.Candidate is { } candidate
            ? $"Version {candidate.Version} · {candidate.Manifest.Channel}"
            : "No update available";
        _releaseNotes.Text = snapshot.Candidate?.ReleaseNotes ?? "";
        _releaseNotes.IsVisible = !string.IsNullOrWhiteSpace(_releaseNotes.Text);
        _error.Text = snapshot.ErrorMessage ?? "";
        _error.IsVisible = !string.IsNullOrWhiteSpace(_error.Text);
        _progress.IsVisible = snapshot.State == UpdateState.Downloading;
        _progress.Progress = snapshot.Progress?.Fraction ?? 0;
        _releaseButton.IsVisible = snapshot.Candidate is not null;
        _downloadButton.IsVisible = snapshot.Candidate is not null
            && snapshot.State is UpdateState.Available or UpdateState.Deferred;
        _snoozeButton.IsVisible = snapshot.Candidate is not null
            && snapshot.State == UpdateState.Available;
        _revealButton.IsVisible = snapshot.Download is not null;
        _applyButton.IsVisible = snapshot.Download is not null && Coordinator is not null;
        _downloadButton.IsEnabled = snapshot.State is UpdateState.Available or UpdateState.Deferred;
        _checkButton.IsEnabled = snapshot.State != UpdateState.Downloading;

        if (snapshot.ShouldNotify)
            _ = PresentNotificationAsync(snapshot);
    }

    private async Task PresentNotificationAsync(UpdateSnapshot snapshot)
    {
        if (Interlocked.Exchange(ref _notificationInFlight, 1) != 0)
            return;
        try
        {
            if (HostActions is { } host)
            {
                if (NotificationMode == UpdateNotificationMode.Popup)
                    await host.ShowPopupAsync(snapshot).ConfigureAwait(false);
                else if (NotificationMode == UpdateNotificationMode.Toast)
                    await host.ShowToastAsync(snapshot).ConfigureAwait(false);
            }

            if (Coordinator is { } coordinator)
                await coordinator.AcknowledgeNotificationAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _notificationInFlight, 0);
        }
    }

    private async Task CheckAsync()
    {
        if (Coordinator is not null)
            await Coordinator.CheckAsync(force: true).ConfigureAwait(false);
    }

    private async Task OpenReleaseAsync()
    {
        if (Coordinator?.Snapshot.Candidate is { } candidate && HostActions is { } host)
            await host.OpenReleaseAsync(candidate.ReleaseUri).ConfigureAwait(false);
    }

    private async Task DownloadAsync()
    {
        if (Coordinator is not null)
            await Coordinator.DownloadAsync().ConfigureAwait(false);
    }

    private async Task SnoozeAsync()
    {
        if (Coordinator is not null)
            await Coordinator.SnoozeAsync().ConfigureAwait(false);
    }

    private async Task RevealAsync()
    {
        if (Coordinator?.Snapshot.Download is { } download && HostActions is { } host)
            await host.RevealDownloadAsync(download.Path).ConfigureAwait(false);
    }

    private async Task ApplyAsync()
    {
        if (Coordinator is not null)
            await Coordinator.ApplyAsync().ConfigureAwait(false);
    }

    private static Label CreateLabel(string text, string automationId)
    {
        var label = new Label { Text = text };
        label.AutomationId = automationId;
        SemanticProperties.SetDescription(label, text);
        return label;
    }

    private static Button CreateButton(string text, string automationId)
    {
        var button = new Button { Text = text, MinimumHeightRequest = 44 };
        button.AutomationId = automationId;
        SemanticProperties.SetDescription(button, text);
        return button;
    }
}
