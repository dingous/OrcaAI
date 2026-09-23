using OrcaAI.Infrastructure;

namespace OrcaAI.ViewModels;

public abstract class BaseViewModel : ObservableObject
{
    private bool _isBusy;
    private string _errorMessage = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        protected set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
}
