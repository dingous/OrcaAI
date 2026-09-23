using OrcaAI.Infrastructure;
using OrcaAI.Pages;
using OrcaAI.Services;

namespace OrcaAI;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;
    private bool _initialized;

    public AppShell()
    {
        InitializeComponent();
        _authService = AppServices.GetRequiredService<IAuthService>();
        Routing.RegisterRoute(nameof(ClientFormPage), typeof(ClientFormPage));
        Routing.RegisterRoute(nameof(QuoteEditorPage), typeof(QuoteEditorPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
            return;

        _initialized = true;
        try
        {
            var session = await _authService.GetSessionAsync();
            await GoToAsync(session?.IsValid == true ? "//app/dashboard" : "//login");
        }
        catch
        {
            await GoToAsync("//login");
        }
    }
}
