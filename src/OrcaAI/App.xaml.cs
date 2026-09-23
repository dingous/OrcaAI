using System.Globalization;

namespace OrcaAI;

public partial class App : Application
{
    public App()
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        InitializeComponent();

        // A identidade visual atual é clara; forçar o tema evita controles
        // parcialmente escuros quando o sistema operacional está em dark mode.
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell());
}
