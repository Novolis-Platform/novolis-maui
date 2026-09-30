using Microsoft.Maui.Graphics;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;
using Novolis.Maui.GraphicalProfile;

namespace Novolis.Maui.Unit;

public sealed class GraphicalProfileTests
{
    [Test]
    [Arguments(nameof(GraphicalProfileColors.BackgroundDark), "#080D1C")]
    [Arguments(nameof(GraphicalProfileColors.BackgroundLight), "#F5F7FC")]
    [Arguments(nameof(GraphicalProfileColors.SurfaceDark), "#111B31")]
    [Arguments(nameof(GraphicalProfileColors.SurfaceLight), "#FFFFFF")]
    [Arguments(nameof(GraphicalProfileColors.RaisedDark), "#172440")]
    [Arguments(nameof(GraphicalProfileColors.RaisedLight), "#EEF3FF")]
    [Arguments(nameof(GraphicalProfileColors.BorderDark), "#263A60")]
    [Arguments(nameof(GraphicalProfileColors.BorderLight), "#D7E0F0")]
    [Arguments(nameof(GraphicalProfileColors.TextDark), "#F4F7FF")]
    [Arguments(nameof(GraphicalProfileColors.TextLight), "#17213A")]
    [Arguments(nameof(GraphicalProfileColors.MutedDark), "#9AAECD")]
    [Arguments(nameof(GraphicalProfileColors.MutedLight), "#5B6B86")]
    [Arguments(nameof(GraphicalProfileColors.AccentDark), "#2FDFFF")]
    [Arguments(nameof(GraphicalProfileColors.AccentLight), "#2FDFFF")]
    [Arguments(nameof(GraphicalProfileColors.AccentFillDark), "#258BFF")]
    [Arguments(nameof(GraphicalProfileColors.AccentFillLight), "#258BFF")]
    [Arguments(nameof(GraphicalProfileColors.OnAccentFillDark), "#FFFFFF")]
    [Arguments(nameof(GraphicalProfileColors.OnAccentFillLight), "#FFFFFF")]
    [Arguments(nameof(GraphicalProfileColors.ActionDark), "#914BFF")]
    [Arguments(nameof(GraphicalProfileColors.ActionLight), "#914BFF")]
    [Arguments(nameof(GraphicalProfileColors.OnActionDark), "#FFFFFF")]
    [Arguments(nameof(GraphicalProfileColors.OnActionLight), "#FFFFFF")]
    [Arguments(nameof(GraphicalProfileColors.ActionSoftDark), "#167C88")]
    [Arguments(nameof(GraphicalProfileColors.ActionSoftLight), "#167C88")]
    [Arguments(nameof(GraphicalProfileColors.WarningDark), "#F0C56A")]
    [Arguments(nameof(GraphicalProfileColors.WarningLight), "#8A5A12")]
    [Arguments(nameof(GraphicalProfileColors.DangerDark), "#FF8D8D")]
    [Arguments(nameof(GraphicalProfileColors.DangerLight), "#C43545")]
    public async Task RoleHex_MatchesGovernanceBundle(string fieldName, string expected)
    {
        var value = typeof(GraphicalProfileColors).GetField(fieldName)?.GetValue(null);
        await Assert.That(value).IsEqualTo(expected);
        await Assert.That(Color.FromArgb(expected)).IsNotNull();
    }

    [Test]
    public async Task ResourceKeys_UseNgpPrefix()
    {
        var background = Profile.BackgroundResourceKey;
        var action = Profile.ActionResourceKey;
        var danger = Profile.DangerResourceKey;
        await Assert.That(background).IsEqualTo("Ngp.Background");
        await Assert.That(action).IsEqualTo("Ngp.Action");
        await Assert.That(danger).IsEqualTo("Ngp.Danger");
    }
}
