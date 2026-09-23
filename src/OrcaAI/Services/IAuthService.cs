using OrcaAI.Models;

namespace OrcaAI.Services;

public interface IAuthService
{
    Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken = default);
    Task<AuthSession> LoginWithGoogleAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync();
}
