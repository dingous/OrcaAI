using System.Windows.Input;
using OrcaAI.Infrastructure;
using OrcaAI.Models;
using OrcaAI.Services;

namespace OrcaAI.ViewModels;

public sealed class SettingsViewModel : BaseViewModel
{
    private readonly IOrcaDataStore _store;
    private string _businessName = string.Empty;
    private string _ownerName = string.Empty;
    private string _document = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _city = string.Empty;
    private int _defaultValidityDays = 7;
    private decimal _defaultLaborValue = 150;
    private string _successMessage = string.Empty;

    public SettingsViewModel(IOrcaDataStore store)
    {
        _store = store;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public string BusinessName { get => _businessName; set => SetProperty(ref _businessName, value); }
    public string OwnerName { get => _ownerName; set => SetProperty(ref _ownerName, value); }
    public string Document { get => _document; set => SetProperty(ref _document, value); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string City { get => _city; set => SetProperty(ref _city, value); }
    public int DefaultValidityDays { get => _defaultValidityDays; set => SetProperty(ref _defaultValidityDays, Math.Clamp(value, 1, 365)); }
    public decimal DefaultLaborValue { get => _defaultLaborValue; set => SetProperty(ref _defaultLaborValue, Math.Max(0, value)); }
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

    public async Task LoadAsync()
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
    }

    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(BusinessName))
        {
            ErrorMessage = "Informe o nome da empresa ou profissional.";
            return;
        }

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
}
