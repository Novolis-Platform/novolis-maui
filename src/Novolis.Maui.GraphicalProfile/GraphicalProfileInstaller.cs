using Microsoft.Maui.Controls;

namespace Novolis.Maui.GraphicalProfile;

/// <summary>Installs the graphical profile into a MAUI application.</summary>
public sealed class GraphicalProfileInstaller
{
    /// <summary>Installs the profile resources into the specified application.</summary>
    public void Install(Application application) =>
        GraphicalProfile.Install(application);
}
