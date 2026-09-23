using OrcaAI.Infrastructure;
using OrcaAI.ViewModels;

namespace OrcaAI.Pages;

public partial class ClientFormPage : ContentPage, IQueryAttributable
{
    private readonly ClientFormViewModel _viewModel;
    private bool _loaded;
    private string? _clientId;

    public ClientFormPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<ClientFormViewModel>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _clientId = query.TryGetValue("clientId", out var value)
            ? Convert.ToString(value)
            : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;

        _loaded = true;
        await _viewModel.LoadAsync(_clientId);
    }
}
