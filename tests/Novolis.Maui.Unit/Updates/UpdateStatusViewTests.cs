using Microsoft.Maui.Controls;
using Novolis.Maui.Updates;

namespace Novolis.Maui.Unit.Updates;

public sealed class UpdateStatusViewTests
{
    [Test]
    public async Task View_exposes_stable_semantic_identity_and_profile_card()
    {
        var view = new UpdateStatusView();

        await Assert.That(view.AutomationId).IsEqualTo("UpdateStatusView");
        await Assert.That(SemanticProperties.GetDescription(view))
            .IsEqualTo("Application updates");
        await Assert.That(view.ShowInline).IsTrue();
        await Assert.That(view.NotificationMode).IsEqualTo(UpdateNotificationMode.Inline);
        await Assert.That(view.Content).IsTypeOf<Border>();
    }

    [Test]
    public async Task View_can_be_hidden_and_switch_to_popup_mode_without_a_handler()
    {
        var view = new UpdateStatusView
        {
            ShowInline = false,
            NotificationMode = UpdateNotificationMode.Popup,
        };

        await Assert.That(view.IsVisible).IsFalse();
        await Assert.That(view.NotificationMode).IsEqualTo(UpdateNotificationMode.Popup);
        view.ReapplyProfileResources();
        await Assert.That(view.Content).IsNotNull();
    }

    [Test]
    public async Task View_child_controls_have_touch_sized_accessible_ids()
    {
        var view = new UpdateStatusView();
        var card = (Border)view.Content!;
        var layout = (VerticalStackLayout)card.Content!;
        var actions = layout.Children.OfType<HorizontalStackLayout>().Single();
        var buttons = actions.Children.OfType<Button>().ToList();

        await Assert.That(buttons).Count().IsEqualTo(6);
        await Assert.That(buttons.All(button => button.MinimumHeightRequest >= 44)).IsTrue();
        await Assert.That(buttons.Select(button => button.AutomationId))
            .Contains("UpdateStatusView.DownloadButton");
        await Assert.That(buttons.Select(button => SemanticProperties.GetDescription(button)))
            .Contains("Download");
    }
}
