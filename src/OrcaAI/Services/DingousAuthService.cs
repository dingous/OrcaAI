using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using OrcaAI.Models;

namespace OrcaAI.Services;

public sealed class DingousAuthService : IAuthService
{
    private const string SessionKey = "orcaai.auth.session.v1";
    private const string GoogleLoginUrl = "https://www.dingous.com.br/auth/orcaai/google";
    private static readonly Uri MobileCallback = new("orcaai://auth");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var json = await SecureStorage.Default.GetAsync(SessionKey);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var session = JsonSerializer.Deserialize<AuthSession>(json, JsonOptions);
            if (session?.IsValid == true)
                return session;
        }
        catch (Exception)
        {
            // SecureStorage pode ficar inválido após troca de backup/chave no dispositivo.
        }

        SecureStorage.Default.Remove(SessionKey);
        return null;
    }

    public async Task<AuthSession> LoginWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        var appState = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

#if WINDOWS
        var parameters = await LoginWithWindowsLoopbackAsync(appState, cancellationToken);
#else
        var authUrl = BuildLoginUrl(MobileCallback.ToString(), appState);
        var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri(authUrl),
            CallbackUrl = MobileCallback,
            PrefersEphemeralWebBrowserSession = false
        });

        var parameters = result.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
#endif

        var session = ParseSession(parameters, appState);
        await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(session, JsonOptions));
        return session;
    }

    public Task LogoutAsync()
    {
        SecureStorage.Default.Remove(SessionKey);
        return Task.CompletedTask;
    }

    private static AuthSession ParseSession(IReadOnlyDictionary<string, string> parameters, string expectedState)
    {
        if (parameters.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException(error);

        if (!parameters.TryGetValue("state", out var state)
            || state.Length != expectedState.Length
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(state),
                Encoding.UTF8.GetBytes(expectedState)))
        {
            throw new InvalidOperationException("A resposta de autenticação não corresponde à solicitação iniciada pelo aplicativo.");
        }

        if (!parameters.TryGetValue("access_token", out var token) || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("O Dingous ChatTrade não retornou um token de acesso.");

        if (!parameters.TryGetValue("expires_at", out var expiresRaw)
            || !DateTimeOffset.TryParse(expiresRaw, out var expiresAt)
            || expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("A validade da sessão retornada pelo servidor é inválida.");
        }

        return new AuthSession
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            Name = Get(parameters, "name") ?? "Usuário",
            Email = Get(parameters, "email") ?? string.Empty,
            PictureUrl = Get(parameters, "picture") ?? string.Empty
        };
    }

    private static string? Get(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.TryGetValue(key, out var value) ? value : null;

    private static string BuildLoginUrl(string callback, string state) =>
        $"{GoogleLoginUrl}?callback={Uri.EscapeDataString(callback)}&state={Uri.EscapeDataString(state)}";

#if WINDOWS
    private static async Task<IReadOnlyDictionary<string, string>> LoginWithWindowsLoopbackAsync(
        string appState,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var callback = $"http://127.0.0.1:{endpoint.Port}/auth";
            var loginUrl = BuildLoginUrl(callback, appState);

            if (!await Launcher.Default.OpenAsync(new Uri(loginUrl)))
                throw new InvalidOperationException("Não foi possível abrir o navegador para entrar com Google.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));

            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true);

            var requestLine = await reader.ReadLineAsync(timeout.Token);
            if (string.IsNullOrWhiteSpace(requestLine))
                throw new InvalidOperationException("Resposta de autenticação vazia.");

            string? line;
            do
            {
                line = await reader.ReadLineAsync(timeout.Token);
            } while (!string.IsNullOrEmpty(line));

            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidOperationException("Resposta de autenticação inválida.");

            var callbackUri = new Uri($"http://127.0.0.1:{endpoint.Port}{parts[1]}");
            if (!callbackUri.IsLoopback || !string.Equals(callbackUri.AbsolutePath.TrimEnd('/'), "/auth", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Callback de autenticação inválido.");

            var values = ParseQuery(callbackUri.Query);

            const string html = "<!doctype html><html lang='pt-BR'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>OrçaAI</title></head><body style='font-family:system-ui;padding:40px;text-align:center;background:#f7f7fc;color:#1d2030'><h2>Login concluído</h2><p>Você já pode voltar ao OrçaAI e fechar esta aba.</p></body></html>";
            var body = Encoding.UTF8.GetBytes(html);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");

            await stream.WriteAsync(header, timeout.Token);
            await stream.WriteAsync(body, timeout.Token);
            await stream.FlushAsync(timeout.Token);

            return values;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
            var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty;
            values[key] = value;
        }

        return values;
    }
#endif
}
