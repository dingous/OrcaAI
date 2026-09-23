using System.Net.Mail;
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
    public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value ?? string.Empty); }
    public string Email { get => _email; set => SetProperty(ref _email, value ?? string.Empty); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value ?? string.Empty); }
    public ICommand SaveCommand { get; }

    public async Task LoadAsync(string? id)
    {
        ClearError();

        if (string.IsNullOrWhiteSpace(id))
            return;

        if (!Guid.TryParse(id, out var parsed))
        {
            SetError("Identificador do cliente inválido.");
            return;
        }

        IsBusy = true;
        RaiseSaveState();

        try
        {
            var client = await _store.GetClientAsync(parsed);
            if (client is null)
            {
                SetError("Cliente não encontrado.");
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
            SetError("Não foi possível carregar o cliente agora. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
            RaiseSaveState();
        }
    }

    private async Task SaveAsync()
    {
        ClearError();

        if (string.IsNullOrWhiteSpace(Name))
        {
            SetError("Informe o nome do cliente.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(Email)
            && !MailAddress.TryCreate(Email.Trim(), out _))
        {
            SetError("Informe um e-mail válido ou deixe o campo em branco.");
            return;
        }

        IsBusy = true;
        RaiseSaveState();

        try
        {
            var existing = _id.HasValue ? await _store.GetClientAsync(_id.Value) : null;
            if (_id.HasValue && existing is null)
            {
                SetError("O cliente não existe mais.");
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
            SetError("Não foi possível salvar o cliente. Tente novamente.", ex);
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
