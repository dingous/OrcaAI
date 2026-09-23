using System.Collections.ObjectModel;
using System.Windows.Input;
using OrcaAI.Infrastructure;
using OrcaAI.Models;
using OrcaAI.Pages;
using OrcaAI.Services;

namespace OrcaAI.ViewModels;

public sealed class ClientsViewModel : BaseViewModel
{
    private readonly IOrcaDataStore _store;
    private string _search = string.Empty;
    private List<Client> _all = [];

    public ClientsViewModel(IOrcaDataStore store)
    {
        _store = store;
        AddCommand = new AsyncRelayCommand(() => NavigateAsync(nameof(ClientFormPage)), () => !IsBusy);
    }

    public ObservableCollection<Client> Items { get; } = [];
    public ICommand AddCommand { get; }
    public string EmptyTitle => string.IsNullOrWhiteSpace(Search) ? "Nenhum cliente ainda" : "Nenhum cliente encontrado";
    public string EmptyMessage => string.IsNullOrWhiteSpace(Search)
        ? "Cadastre o primeiro cliente para acelerar seus próximos orçamentos."
        : "Tente outro nome, telefone ou e-mail.";

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? string.Empty))
                ApplyFilter();
        }
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        RaiseActionStates();

        try
        {
            ClearError();
            _all = (await _store.GetClientsAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            SetError("Não foi possível carregar os clientes agora. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
            RaiseActionStates();
        }
    }

    public Task EditAsync(Client client) =>
        IsBusy
            ? Task.CompletedTask
            : NavigateAsync($"{nameof(ClientFormPage)}?clientId={client.Id}");

    public async Task DeleteAsync(Client client)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        RaiseActionStates();

        try
        {
            ClearError();
            await _store.DeleteClientAsync(client.Id);
            _all.RemoveAll(x => x.Id == client.Id);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            SetError("Não foi possível excluir o cliente. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
            RaiseActionStates();
        }
    }

    private void RaiseActionStates() =>
        (AddCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

    private async Task NavigateAsync(string route)
    {
        try
        {
            ClearError();
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            SetError("Não foi possível abrir o cliente. Tente novamente.", ex);
        }
    }

    private void ApplyFilter()
    {
        var query = Search.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _all
            : _all.Where(x => x.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                           || x.Phone.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                           || x.Email.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

        Items.Clear();
        foreach (var item in filtered)
            Items.Add(item);

        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}
