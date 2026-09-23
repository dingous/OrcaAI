using Microsoft.Extensions.DependencyInjection;

namespace OrcaAI.Infrastructure;

public static class AppServices
{
    public static IServiceProvider Services { get; set; } = default!;

    public static T GetRequiredService<T>() where T : notnull =>
        Services.GetRequiredService<T>();
}
