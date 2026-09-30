using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace Novolis.Maui.GraphicalProfile;

/// <summary>MAUI builder integration for the required Novolis graphical profile.</summary>
public static class GraphicalProfileBuilderExtensions
{
    /// <summary>Registers the graphical profile installer with the MAUI host.</summary>
    public static MauiAppBuilder UseGraphicalProfile(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<GraphicalProfileInstaller>();
        return builder;
    }
}
