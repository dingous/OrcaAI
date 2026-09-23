using OrcaAI.Infrastructure;
using OrcaAI.ViewModels;

namespace OrcaAI.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<SettingsViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
