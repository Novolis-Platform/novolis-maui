using Microsoft.Maui.Graphics;
using Novolis.Maui.GraphicalProfile;

namespace Novolis.Maui.Unit.GraphicalProfile;

public sealed class GraphicalProfileTests
{
    [Test]
    public async Task DarkPalette_MatchesGovernanceBundle()
    {
        await Assert.That(GraphicalProfileColors.BackgroundDark).IsEqualTo("#080D1C");
        await Assert.That(GraphicalProfileColors.AccentDark).IsEqualTo("#2FDFFF");
        await Assert.That(GraphicalProfileColors.AccentFillDark).IsEqualTo("#258BFF");
        await Assert.That(GraphicalProfileColors.ActionDark).IsEqualTo("#914BFF");
    }

    [Test]
    public async Task LightPalette_MatchesGovernanceBundle()
    {
        await Assert.That(GraphicalProfileColors.BackgroundLight).IsEqualTo("#F5F7FC");
        await Assert.That(GraphicalProfileColors.SurfaceLight).IsEqualTo("#FFFFFF");
        await Assert.That(GraphicalProfileColors.RaisedLight).IsEqualTo("#EEF3FF");
    }

    [Test]
    public async Task ColorStrings_AreAcceptedByMaui()
    {
        await Assert.That(Color.FromArgb(GraphicalProfileColors.BackgroundDark)).IsNotNull();
        await Assert.That(Color.FromArgb(GraphicalProfileColors.ActionLight)).IsNotNull();
    }
}
