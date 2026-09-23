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
        ClearError();
        IsBusy = true;
        RaiseLoginState();

        try
        {
            await _authService.LoginWithGoogleAsync();
            await Shell.Current.GoToAsync("//app/dashboard");
        }
        catch (TaskCanceledException)
        {
            SetError("Login cancelado.");
        }
        catch (OperationCanceledException)
        {
            SetError("O tempo para concluir o login terminou. Tente novamente.");
        }
        catch (InvalidOperationException ex)
        {
            SetError(ex.Message, ex);
        }
        catch (Exception ex)
        {
            SetError("Não foi possível entrar com Google agora. Tente novamente.", ex);
        }
        finally
        {
            IsBusy = false;
            RaiseLoginState();
        }
    }

    private void RaiseLoginState() =>
        (LoginCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
}
