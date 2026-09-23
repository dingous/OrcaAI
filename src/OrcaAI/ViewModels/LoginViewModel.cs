using System.Windows.Input;
using OrcaAI.Infrastructure;
using OrcaAI.Services;

namespace OrcaAI.ViewModels;

public sealed class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
    }

    public ICommand LoginCommand { get; }

    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            await _authService.LoginWithGoogleAsync();
            await Shell.Current.GoToAsync("//app/dashboard");
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Login cancelado.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "O tempo para concluir o login terminou. Tente novamente.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível entrar com Google. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            if (LoginCommand is AsyncRelayCommand command)
                command.RaiseCanExecuteChanged();
        }
    }
}
