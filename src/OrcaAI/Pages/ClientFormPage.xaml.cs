using OrcaAI.Infrastructure;
using OrcaAI.ViewModels;

namespace OrcaAI.Pages;

[QueryProperty(nameof(ClientId), "clientId")]
public partial class ClientFormPage : ContentPage
{
    private readonly ClientFormViewModel _viewModel;
    private bool _loaded;
    private string? _clientId;

    public ClientFormPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<ClientFormViewModel>();
    }

    public string? ClientId
    {
        get => _clientId;
        set => _clientId = value;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        await _viewModel.LoadAsync(_clientId);
    }
}
