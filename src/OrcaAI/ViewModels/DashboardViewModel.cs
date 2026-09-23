using System.Globalization;
using System.Windows.Input;
using OrcaAI.Infrastructure;
using OrcaAI.Pages;
using OrcaAI.Services;

namespace OrcaAI.ViewModels;

public sealed class DashboardViewModel : BaseViewModel
{
    private readonly IOrcaDataStore _store;
    private string _clients = "0";
    private string _quotesThisMonth = "0";
    private string _pending = "0";
    private string _approvedValue = "R$ 0,00";

    public DashboardViewModel(IOrcaDataStore store)
    {
        _store = store;
        NewQuoteCommand = new AsyncRelayCommand(() => Shell.Current.GoToAsync(nameof(QuoteEditorPage)));
        AddClientCommand = new AsyncRelayCommand(() => Shell.Current.GoToAsync(nameof(ClientFormPage)));
        ViewQuotesCommand = new AsyncRelayCommand(() => Shell.Current.GoToAsync("//app/quotes"));
    }

    public string Clients { get => _clients; private set => SetProperty(ref _clients, value); }
    public string QuotesThisMonth { get => _quotesThisMonth; private set => SetProperty(ref _quotesThisMonth, value); }
    public string Pending { get => _pending; private set => SetProperty(ref _pending, value); }
    public string ApprovedValue { get => _approvedValue; private set => SetProperty(ref _approvedValue, value); }

    public ICommand NewQuoteCommand { get; }
    public ICommand AddClientCommand { get; }
    public ICommand ViewQuotesCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            var stats = await _store.GetDashboardStatsAsync();
            Clients = stats.Clients.ToString(CultureInfo.CurrentCulture);
            QuotesThisMonth = stats.QuotesThisMonth.ToString(CultureInfo.CurrentCulture);
            Pending = stats.Pending.ToString(CultureInfo.CurrentCulture);
            ApprovedValue = stats.ApprovedValue.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar o resumo: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
