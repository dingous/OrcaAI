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
        AddCommand = new AsyncRelayCommand(() => Shell.Current.GoToAsync(nameof(ClientFormPage)));
    }

    public ObservableCollection<Client> Items { get; } = [];
    public ICommand AddCommand { get; }

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value))
                ApplyFilter();
        }
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            _all = (await _store.GetClientsAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar os clientes: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task EditAsync(Client client) =>
        Shell.Current.GoToAsync($"{nameof(ClientFormPage)}?clientId={client.Id}");

    public async Task DeleteAsync(Client client)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            await _store.DeleteClientAsync(client.Id);
            _all.RemoveAll(x => x.Id == client.Id);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível excluir o cliente: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
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
    }
}
