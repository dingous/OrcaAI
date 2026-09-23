using System.Diagnostics;
using OrcaAI.Infrastructure;

namespace OrcaAI.ViewModels;

public abstract class BaseViewModel : ObservableObject
{
    private bool _isBusy;
    private string _errorMessage = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        protected set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    public bool IsNotBusy => !IsBusy;

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

    protected void ClearError() => ErrorMessage = string.Empty;

    protected void SetError(string userMessage, Exception? exception = null)
    {
        if (exception is not null)
            Debug.WriteLine(exception);

        ErrorMessage = userMessage;
    }
}
