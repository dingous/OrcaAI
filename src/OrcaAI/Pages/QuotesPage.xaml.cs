using OrcaAI.Infrastructure;
using OrcaAI.Models;
using OrcaAI.ViewModels;

namespace OrcaAI.Pages;

public partial class QuotesPage : ContentPage
{
    private readonly QuotesViewModel _viewModel;

    public QuotesPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<QuotesViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Quote quote })
            await _viewModel.EditAsync(quote);
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Quote quote }) return;
        var confirm = await DisplayAlert("Excluir orçamento", $"Excluir {quote.Number}?", "Excluir", "Cancelar");
        if (confirm) await _viewModel.DeleteAsync(quote);
    }
}
