using Microsoft.Extensions.Logging;
using OrcaAI.Infrastructure;
using OrcaAI.Services;
using OrcaAI.ViewModels;

namespace OrcaAI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<IOrcaDataStore, JsonOrcaDataStore>();
        builder.Services.AddSingleton<IAiQuoteDraftService, LocalAiQuoteDraftService>();
        builder.Services.AddSingleton<IPdfService, SimplePdfService>();

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<ClientsViewModel>();
        builder.Services.AddTransient<ClientFormViewModel>();
        builder.Services.AddTransient<QuotesViewModel>();
        builder.Services.AddTransient<QuoteEditorViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        var app = builder.Build();
        AppServices.Services = app.Services;
        return app;
    }
}
