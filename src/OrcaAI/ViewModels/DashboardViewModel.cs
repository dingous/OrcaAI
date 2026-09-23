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
        NewQuoteCommand = new AsyncRelayCommand(() => NavigateAsync(nameof(QuoteEditorPage)), () => !IsBusy);
        AddClientCommand = new AsyncRelayCommand(() => NavigateAsync(nameof(ClientFormPage)), () => !IsBusy);
        ViewQuotesCommand = new AsyncRelayCommand(() => NavigateAsync("//app/quotes"), () => !IsBusy);
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
        if (IsBusy)
            return;

        SetBusy(true);
        try
        {
            ClearError();
            var stats = await _store.GetDashboardStatsAsync();
            Clients = stats.Clients.ToString(CultureInfo.CurrentCulture);
            QuotesThisMonth = stats.QuotesThisMonth.ToString(CultureInfo.CurrentCulture);
            Pending = stats.Pending.ToString(CultureInfo.CurrentCulture);
            ApprovedValue = stats.ApprovedValue.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
        }
        catch (Exception ex)
        {
            SetError("Não foi possível carregar o resumo agora. Tente novamente.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task NavigateAsync(string route)
    {
        if (IsBusy)
            return;

        SetBusy(true);
        try
        {
            ClearError();
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            SetError("Não foi possível abrir esta tela. Tente novamente.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        IsBusy = value;
        (NewQuoteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (AddClientCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ViewQuotesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}
