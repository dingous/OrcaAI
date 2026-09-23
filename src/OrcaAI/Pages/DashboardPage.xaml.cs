using OrcaAI.Infrastructure;
using OrcaAI.ViewModels;

namespace OrcaAI.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<DashboardViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
