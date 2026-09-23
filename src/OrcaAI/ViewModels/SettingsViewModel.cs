using System.Net.Mail;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using OrcaAI.Infrastructure;
using OrcaAI.Models;
using OrcaAI.Services;

namespace OrcaAI.ViewModels;

public sealed class SettingsViewModel : BaseViewModel
{
    private static readonly Uri PrivacyPolicyUri = new("https://www.dingous.com.br/privacy-policy");
    private static readonly Uri TermsUri = new("https://www.dingous.com.br/term-service");
    private static readonly Uri AccountDeletionUri = new("https://www.dingous.com.br/exclusao-de-conta");

    private readonly IOrcaDataStore _store;
    private readonly IAuthService _authService;
    private string _businessName = string.Empty;
    private string _ownerName = string.Empty;
    private string _document = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _city = string.Empty;
    private int _defaultValidityDays = 7;
    private decimal _defaultLaborValue;
    private string _successMessage = string.Empty;
    private string _userName = "Usuário";
    private string _userEmail = string.Empty;
    private string _userInitials = "?";

    public SettingsViewModel(IOrcaDataStore store, IAuthService authService)
    {
        _store = store;
        _authService = authService;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        LogoutCommand = new AsyncRelayCommand(LogoutAsync, () => !IsBusy);
        OpenPrivacyCommand = new AsyncRelayCommand(() => OpenExternalAsync(PrivacyPolicyUri), () => !IsBusy);
        OpenTermsCommand = new AsyncRelayCommand(() => OpenExternalAsync(TermsUri), () => !IsBusy);
        RequestAccountDeletionCommand = new AsyncRelayCommand(() => OpenExternalAsync(AccountDeletionUri), () => !IsBusy);
    }

    public string BusinessName { get => _businessName; set => SetProperty(ref _businessName, value ?? string.Empty); }
    public string OwnerName { get => _ownerName; set => SetProperty(ref _ownerName, value ?? string.Empty); }
    public string Document { get => _document; set => SetProperty(ref _document, value ?? string.Empty); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value ?? string.Empty); }
    public string Email { get => _email; set => SetProperty(ref _email, value ?? string.Empty); }
    public string City { get => _city; set => SetProperty(ref _city, value ?? string.Empty); }
    public int DefaultValidityDays { get => _defaultValidityDays; set => SetProperty(ref _defaultValidityDays, Math.Clamp(value, 1, 365)); }
    public decimal DefaultLaborValue { get => _defaultLaborValue; set => SetProperty(ref _defaultLaborValue, Math.Max(0, value)); }
    public string UserName { get => _userName; private set => SetProperty(ref _userName, value); }
    public string UserEmail { get => _userEmail; private set => SetProperty(ref _userEmail, value); }
    public string UserInitials { get => _userInitials; private set => SetProperty(ref _userInitials, value); }

    public string SuccessMessage
    {
        get => _successMessage;
        private set
        {
            if (SetProperty(ref _successMessage, value))
                OnPropertyChanged(nameof(HasSuccess));
        }
    }

    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);

    public ICommand SaveCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand OpenPrivacyCommand { get; }
    public ICommand OpenTermsCommand { get; }
    public ICommand RequestAccountDeletionCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ClearError();
        SuccessMessage = string.Empty;

        try
        {
            var profile = await _store.GetBusinessProfileAsync();
            BusinessName = profile.BusinessName;
            OwnerName = profile.OwnerName;
            Document = profile.Document;
            Phone = profile.Phone;
            Email = profile.Email;
            City = profile.City;
            DefaultValidityDays = profile.DefaultValidityDays;
            DefaultLaborValue = profile.DefaultLaborValue;

            var session = await _authService.GetSessionAsync();
            if (session is not null)
            {
                UserName = session.Name;
                UserEmail = session.Email;
                UserInitials = session.Initials;
            }
        }
        catch (Exception ex)
        {
            SetError("Não foi possível carregar seus dados agora. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task SaveAsync()
    {
        ClearError();
        SuccessMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(BusinessName))
        {
            SetError("Informe o nome da empresa ou profissional.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(Email)
            && !MailAddress.TryCreate(Email.Trim(), out _))
        {
            SetError("Informe um e-mail válido ou deixe o campo em branco.");
            return;
        }

        IsBusy = true;
        RaiseCommandStates();

        try
        {
            await _store.SaveBusinessProfileAsync(new BusinessProfile
            {
                BusinessName = BusinessName.Trim(),
                OwnerName = OwnerName.Trim(),
                Document = Document.Trim(),
                Phone = Phone.Trim(),
                Email = Email.Trim(),
                City = City.Trim(),
                DefaultValidityDays = DefaultValidityDays,
                DefaultLaborValue = DefaultLaborValue
            });

            SuccessMessage = "Dados salvos.";
        }
        catch (Exception ex)
        {
            SetError("Não foi possível salvar seus dados. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task LogoutAsync()
    {
        ClearError();
        IsBusy = true;
        RaiseCommandStates();

        try
        {
            await _authService.LogoutAsync();
            await Shell.Current.GoToAsync("//login");
        }
        catch (Exception ex)
        {
            SetError("Não foi possível sair da conta agora. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task OpenExternalAsync(Uri uri)
    {
        if (IsBusy)
            return;

        ClearError();
        IsBusy = true;
        RaiseCommandStates();

        try
        {
            if (!await Launcher.Default.OpenAsync(uri))
                SetError("Não foi possível abrir a página no navegador.");
        }
        catch (Exception ex)
        {
            SetError("Não foi possível abrir a página no navegador. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private void RaiseCommandStates()
    {
        (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (LogoutCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (OpenPrivacyCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (OpenTermsCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RequestAccountDeletionCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}
