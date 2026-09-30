using Microsoft.Maui.Graphics;
using Profile = Novolis.Maui.GraphicalProfile.GraphicalProfile;
using Novolis.Maui.GraphicalProfile;

namespace Novolis.Maui.Unit;

public sealed class GraphicalProfileTests
{
    [Test]
    [Arguments(nameof(GraphicalProfileColors.BackgroundDark), "#010D18")]
    [Arguments(nameof(GraphicalProfileColors.BackgroundLight), "#F4FDFF")]
    [Arguments(nameof(GraphicalProfileColors.SurfaceDark), "#051730")]
    [Arguments(nameof(GraphicalProfileColors.SurfaceLight), "#FBFEFF")]
    [Arguments(nameof(GraphicalProfileColors.RaisedDark), "#072041")]
    [Arguments(nameof(GraphicalProfileColors.RaisedLight), "#EEF8FF")]
    [Arguments(nameof(GraphicalProfileColors.BorderDark), "#093D6F")]
    [Arguments(nameof(GraphicalProfileColors.BorderLight), "#D0E9FF")]
    [Arguments(nameof(GraphicalProfileColors.TextDark), "#E6FBFF")]
    [Arguments(nameof(GraphicalProfileColors.TextLight), "#0E346C")]
    [Arguments(nameof(GraphicalProfileColors.MutedDark), "#2AA5FF")]
    [Arguments(nameof(GraphicalProfileColors.MutedLight), "#0677D9")]
    [Arguments(nameof(GraphicalProfileColors.AccentDark), "#2FDFFF")]
    [Arguments(nameof(GraphicalProfileColors.AccentLight), "#2FDFFF")]
    [Arguments(nameof(GraphicalProfileColors.AccentFillDark), "#237CFF")]
    [Arguments(nameof(GraphicalProfileColors.AccentFillLight), "#237CFF")]
    [Arguments(nameof(GraphicalProfileColors.OnAccentFillDark), "#EFFDFF")]
    [Arguments(nameof(GraphicalProfileColors.OnAccentFillLight), "#EFFDFF")]
    [Arguments(nameof(GraphicalProfileColors.ActionDark), "#8F37FF")]
    [Arguments(nameof(GraphicalProfileColors.ActionLight), "#8F37FF")]
    [Arguments(nameof(GraphicalProfileColors.OnActionDark), "#EFFDFF")]
    [Arguments(nameof(GraphicalProfileColors.OnActionLight), "#EFFDFF")]
    [Arguments(nameof(GraphicalProfileColors.ActionSoftDark), "#0BA8FF")]
    [Arguments(nameof(GraphicalProfileColors.ActionSoftLight), "#0BA8FF")]
    [Arguments(nameof(GraphicalProfileColors.WarningDark), "#35D8FF")]
    [Arguments(nameof(GraphicalProfileColors.WarningLight), "#0677D9")]
    [Arguments(nameof(GraphicalProfileColors.DangerDark), "#B246FF")]
    [Arguments(nameof(GraphicalProfileColors.DangerLight), "#6138D9")]
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
