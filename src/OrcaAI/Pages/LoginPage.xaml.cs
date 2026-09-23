using OrcaAI.Infrastructure;
using OrcaAI.ViewModels;

namespace OrcaAI.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = AppServices.GetRequiredService<LoginViewModel>();
    }
}
