using OrcaAI.Infrastructure;
using OrcaAI.Models;
using OrcaAI.ViewModels;

namespace OrcaAI.Pages;

public partial class ClientsPage : ContentPage
{
    private readonly ClientsViewModel _viewModel;

    public ClientsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<ClientsViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Client client })
            await _viewModel.EditAsync(client);
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Client client }) return;
        var confirm = await DisplayAlert("Excluir cliente", $"Excluir {client.Name}?", "Excluir", "Cancelar");
        if (confirm) await _viewModel.DeleteAsync(client);
    }
}
