using OrcaAI.Pages;

namespace OrcaAI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(ClientFormPage), typeof(ClientFormPage));
        Routing.RegisterRoute(nameof(QuoteEditorPage), typeof(QuoteEditorPage));
    }
}
