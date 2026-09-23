using System.Windows.Input;
using OrcaAI.Infrastructure;
using OrcaAI.Models;
using OrcaAI.Services;

namespace OrcaAI.ViewModels;

public sealed class ClientFormViewModel : BaseViewModel
{
    private readonly IOrcaDataStore _store;
    private Guid? _id;
    private string _name = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _notes = string.Empty;
    private string _title = "Novo cliente";

    public ClientFormViewModel(IOrcaDataStore store)
    {
        _store = store;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
    }

    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public ICommand SaveCommand { get; }

    public async Task LoadAsync(string? id)
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(id))
            return;

        if (!Guid.TryParse(id, out var parsed))
        {
            ErrorMessage = "Identificador do cliente inválido.";
            return;
        }

        IsBusy = true;
        RaiseSaveState();
        try
        {
            var client = await _store.GetClientAsync(parsed);
            if (client is null)
            {
                ErrorMessage = "Cliente não encontrado.";
                return;
            }

            _id = client.Id;
            Title = "Editar cliente";
            Name = client.Name;
            Phone = client.Phone;
            Email = client.Email;
            Notes = client.Notes;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar o cliente: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseSaveState();
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Informe o nome do cliente.";
            return;
        }

        IsBusy = true;
        RaiseSaveState();
        try
        {
            var existing = _id.HasValue ? await _store.GetClientAsync(_id.Value) : null;
            if (_id.HasValue && existing is null)
            {
                ErrorMessage = "O cliente não existe mais.";
                return;
            }

            var client = existing ?? new Client();
            client.Name = Name.Trim();
            client.Phone = Phone.Trim();
            client.Email = Email.Trim();
            client.Notes = Notes.Trim();
            await _store.SaveClientAsync(client);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível salvar: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseSaveState();
        }
    }

    private void RaiseSaveState() =>
        (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
}
