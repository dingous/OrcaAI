using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Storage;
using OrcaAI.Models;

namespace OrcaAI.Services;

public sealed class JsonOrcaDataStore : IOrcaDataStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAuthService _authService;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string _legacyFilePath;
    private DataEnvelope? _cache;
    private string? _cachePath;

    public JsonOrcaDataStore(IAuthService authService)
    {
        _authService = authService;
        _legacyFilePath = Path.Combine(FileSystem.AppDataDirectory, "orcaai-data.json");
    }

    public async Task<IReadOnlyList<Client>> GetClientsAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync(cancellationToken);
        return data.Clients.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).Select(CloneClient).ToList();
    }

    public async Task<Client?> GetClientAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync(cancellationToken);
        return data.Clients.Where(x => x.Id == id).Select(CloneClient).FirstOrDefault();
    }

    public async Task SaveClientAsync(Client client, CancellationToken cancellationToken = default)
    {
        await MutateAsync(data =>
        {
            var index = data.Clients.FindIndex(x => x.Id == client.Id);
            var previousName = index >= 0 ? data.Clients[index].Name : null;
            var now = DateTime.UtcNow;

            client.UpdatedAtUtc = now;

            if (index >= 0)
                data.Clients[index] = CloneClient(client);
            else
                data.Clients.Add(CloneClient(client));

            if (index >= 0
                && !string.Equals(previousName, client.Name, StringComparison.CurrentCulture)
                && !string.IsNullOrWhiteSpace(client.Name))
            {
                foreach (var quote in data.Quotes.Where(x => x.ClientId == client.Id))
                {
                    quote.ClientName = client.Name;
                    quote.UpdatedAtUtc = now;
                }
            }
        }, cancellationToken);
    }

    public Task DeleteClientAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(data =>
        {
            var client = data.Clients.FirstOrDefault(x => x.Id == id);
            if (client is null)
                return;

            data.Clients.RemoveAll(x => x.Id == id);

            foreach (var quote in data.Quotes.Where(x => x.ClientId == id))
            {
                if (string.IsNullOrWhiteSpace(quote.ClientName))
                    quote.ClientName = client.Name;

                quote.ClientId = null;
                quote.UpdatedAtUtc = DateTime.UtcNow;
            }
        }, cancellationToken);

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync(cancellationToken);
        return data.Quotes.OrderByDescending(x => x.UpdatedAtUtc).Select(CloneQuote).ToList();
    }

    public async Task<Quote?> GetQuoteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync(cancellationToken);
        return data.Quotes.Where(x => x.Id == id).Select(CloneQuote).FirstOrDefault();
    }

    public async Task SaveQuoteAsync(Quote quote, CancellationToken cancellationToken = default)
    {
        await MutateAsync(data =>
        {
            var index = data.Quotes.FindIndex(x => x.Id == quote.Id);
            quote.UpdatedAtUtc = DateTime.UtcNow;
            if (index >= 0)
                data.Quotes[index] = CloneQuote(quote);
            else
                data.Quotes.Add(CloneQuote(quote));
        }, cancellationToken);
    }

    public Task DeleteQuoteAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(data => data.Quotes.RemoveAll(x => x.Id == id), cancellationToken);

    public async Task<BusinessProfile> GetBusinessProfileAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync(cancellationToken);
        return CloneProfile(data.BusinessProfile);
    }

    public Task SaveBusinessProfileAsync(BusinessProfile profile, CancellationToken cancellationToken = default) =>
        MutateAsync(data => data.BusinessProfile = CloneProfile(profile), cancellationToken);

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync(cancellationToken);
        var now = DateTime.Now;
        var quotesThisMonth = data.Quotes.Count(x =>
        {
            var createdLocal = x.CreatedAtUtc.ToLocalTime();
            return createdLocal.Year == now.Year && createdLocal.Month == now.Month;
        });
        var pending = data.Quotes.Count(x => x.Status is QuoteStatus.Draft or QuoteStatus.Sent);
        var approved = 0m;
        foreach (var quote in data.Quotes.Where(x => x.Status is QuoteStatus.Approved or QuoteStatus.Completed))
        {
            var value = quote.Total;
            if (value <= 0)
                continue;

            if (approved > decimal.MaxValue - value)
            {
                approved = decimal.MaxValue;
                break;
            }

            approved += value;
        }

        return new DashboardStats(data.Clients.Count, quotesThisMonth, pending, approved);
    }

    private async Task<DataEnvelope> GetDataAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var filePath = await ResolveFilePathUnsafeAsync(cancellationToken);
            await using var processLock = await AcquireProcessLockAsync(filePath, cancellationToken);

            _cache = null;
            _cachePath = filePath;

            if (!File.Exists(filePath))
            {
                _cache = new DataEnvelope();
                await PersistUnsafeAsync(_cache, filePath, cancellationToken);
                return CloneEnvelope(_cache);
            }

            try
            {
                await using var stream = File.OpenRead(filePath);
                _cache = await JsonSerializer.DeserializeAsync<DataEnvelope>(stream, _jsonOptions, cancellationToken)
                         ?? new DataEnvelope();

                if (NormalizeData(_cache))
                    await PersistUnsafeAsync(_cache, filePath, cancellationToken);
            }
            catch (JsonException)
            {
                BackupCorruptFile(filePath);
                _cache = new DataEnvelope();
                await PersistUnsafeAsync(_cache, filePath, cancellationToken);
            }

            return CloneEnvelope(_cache);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task MutateAsync(Action<DataEnvelope> mutation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var filePath = await ResolveFilePathUnsafeAsync(cancellationToken);
            await using var processLock = await AcquireProcessLockAsync(filePath, cancellationToken);

            _cache = null;
            _cachePath = filePath;

            if (File.Exists(filePath))
            {
                try
                {
                    await using var stream = File.OpenRead(filePath);
                    _cache = await JsonSerializer.DeserializeAsync<DataEnvelope>(stream, _jsonOptions, cancellationToken);
                }
                catch (JsonException)
                {
                    BackupCorruptFile(filePath);
                    _cache = null;
                }
            }

            _cache ??= new DataEnvelope();
            NormalizeData(_cache);

            mutation(_cache);
            NormalizeData(_cache);
            await PersistUnsafeAsync(_cache, filePath, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> ResolveFilePathUnsafeAsync(CancellationToken cancellationToken)
    {
        var session = await _authService.GetSessionAsync(cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.Email))
            throw new InvalidOperationException("É necessário entrar novamente para acessar os dados locais.");

        var normalizedEmail = session.Email.Trim().ToLowerInvariant();
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail))).ToLowerInvariant();
        var scopedPath = Path.Combine(FileSystem.AppDataDirectory, $"orcaai-data-{digest[..24]}.json");

        if (!File.Exists(scopedPath) && File.Exists(_legacyFilePath))
        {
            try
            {
                File.Move(_legacyFilePath, scopedPath);
            }
            catch (IOException) when (File.Exists(scopedPath))
            {
                // Outra instância terminou a migração primeiro; o arquivo de destino é a fonte correta.
            }
        }

        return scopedPath;
    }

    private static async Task<FileStream> AcquireProcessLockAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var lockPath = filePath + ".lock";
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

        IOException? lastError = null;

        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    useAsync: true);
            }
            catch (IOException ex)
            {
                lastError = ex;
                await Task.Delay(50, cancellationToken);
            }
        }

        throw new IOException(
            "Não foi possível acessar os dados locais porque outra instância ainda está gravando.",
            lastError);
    }

    private static DataEnvelope CloneEnvelope(DataEnvelope source) => new()
    {
        Clients = source.Clients.Select(CloneClient).ToList(),
        Quotes = source.Quotes.Select(CloneQuote).ToList(),
        BusinessProfile = CloneProfile(source.BusinessProfile)
    };

    private static void BackupCorruptFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return;

            var backupPath = $"{filePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(filePath, backupPath, true);
        }
        catch
        {
            // A recuperação continua mesmo se o backup não puder ser criado.
        }
    }

    private static bool NormalizeData(DataEnvelope data)
    {
        var changed = false;

        if (data.Clients is null)
        {
            data.Clients = [];
            changed = true;
        }

        if (data.Quotes is null)
        {
            data.Quotes = [];
            changed = true;
        }

        if (data.BusinessProfile is null)
        {
            data.BusinessProfile = new BusinessProfile();
            changed = true;
        }

        var clientCount = data.Clients.Count;
        data.Clients.RemoveAll(static client => client is null);
        if (data.Clients.Count != clientCount)
            changed = true;

        var quoteCount = data.Quotes.Count;
        data.Quotes.RemoveAll(static quote => quote is null);
        if (data.Quotes.Count != quoteCount)
            changed = true;

        var clientIds = new HashSet<Guid>();
        foreach (var client in data.Clients)
        {
            if (client.Id == Guid.Empty || !clientIds.Add(client.Id))
            {
                do
                {
                    client.Id = Guid.NewGuid();
                }
                while (!clientIds.Add(client.Id));

                changed = true;
            }

            client.Name = NormalizeString(client.Name, ref changed);
            client.Phone = NormalizeString(client.Phone, ref changed);
            client.Email = NormalizeString(client.Email, ref changed);
            client.Notes = NormalizeString(client.Notes, ref changed);

            if (client.CreatedAtUtc == default)
            {
                client.CreatedAtUtc = DateTime.UtcNow;
                changed = true;
            }

            if (client.UpdatedAtUtc == default)
            {
                client.UpdatedAtUtc = client.CreatedAtUtc;
                changed = true;
            }
        }

        var quoteIds = new HashSet<Guid>();
        var quoteNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var quote in data.Quotes)
        {
            if (quote.Id == Guid.Empty || !quoteIds.Add(quote.Id))
            {
                do
                {
                    quote.Id = Guid.NewGuid();
                }
                while (!quoteIds.Add(quote.Id));

                changed = true;
            }

            quote.Number = NormalizeString(quote.Number, ref changed).Trim();
            if (string.IsNullOrWhiteSpace(quote.Number) || !quoteNumbers.Add(quote.Number))
            {
                var stamp = quote.CreatedAtUtc == default ? DateTime.UtcNow : quote.CreatedAtUtc;
                quote.Number = $"ORC-{stamp.ToLocalTime():yyMMdd-HHmmss}-{quote.Id.ToString("N")[..6].ToUpperInvariant()}";
                quoteNumbers.Add(quote.Number);
                changed = true;
            }

            if (quote.ClientId.HasValue && !clientIds.Contains(quote.ClientId.Value))
            {
                quote.ClientId = null;
                changed = true;
            }

            quote.ClientName = NormalizeString(quote.ClientName, ref changed);
            quote.Title = NormalizeString(quote.Title, ref changed);
            quote.Description = NormalizeString(quote.Description, ref changed);
            quote.Notes = NormalizeString(quote.Notes, ref changed);

            if (quote.Items is null)
            {
                quote.Items = [];
                changed = true;
            }

            var itemCount = quote.Items.Count;
            quote.Items.RemoveAll(static item => item is null);
            if (quote.Items.Count != itemCount)
                changed = true;

            foreach (var item in quote.Items)
                item.Description = NormalizeString(item.Description, ref changed);

            if (!Enum.IsDefined(typeof(QuoteStatus), quote.Status))
            {
                quote.Status = QuoteStatus.Draft;
                changed = true;
            }

            if (quote.Discount < 0)
            {
                quote.Discount = 0;
                changed = true;
            }

            if (quote.CreatedAtUtc == default)
            {
                quote.CreatedAtUtc = DateTime.UtcNow;
                changed = true;
            }

            if (quote.UpdatedAtUtc == default)
            {
                quote.UpdatedAtUtc = quote.CreatedAtUtc;
                changed = true;
            }

            if (quote.ValidUntil == default)
            {
                quote.ValidUntil = DateTime.Today.AddDays(7);
                changed = true;
            }
        }

        var profile = data.BusinessProfile;
        profile.BusinessName = NormalizeString(profile.BusinessName, ref changed);
        profile.OwnerName = NormalizeString(profile.OwnerName, ref changed);
        profile.Document = NormalizeString(profile.Document, ref changed);
        profile.Phone = NormalizeString(profile.Phone, ref changed);
        profile.Email = NormalizeString(profile.Email, ref changed);
        profile.City = NormalizeString(profile.City, ref changed);

        if (profile.DefaultValidityDays is < 1 or > 365)
        {
            profile.DefaultValidityDays = 7;
            changed = true;
        }

        if (profile.DefaultLaborValue < 0)
        {
            profile.DefaultLaborValue = 0;
            changed = true;
        }

        var untouchedLegacyDefaults =
            string.Equals(profile.BusinessName, "Minha empresa", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(profile.OwnerName)
            && string.IsNullOrWhiteSpace(profile.Document)
            && string.IsNullOrWhiteSpace(profile.Phone)
            && string.IsNullOrWhiteSpace(profile.Email)
            && string.IsNullOrWhiteSpace(profile.City)
            && profile.DefaultLaborValue == 150m;

        if (untouchedLegacyDefaults)
        {
            profile.BusinessName = string.Empty;
            profile.DefaultLaborValue = 0m;
            changed = true;
        }

        return changed;
    }

    private static string NormalizeString(string? value, ref bool changed)
    {
        if (value is not null)
            return value;

        changed = true;
        return string.Empty;
    }

    private static Client CloneClient(Client source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Phone = source.Phone,
        Email = source.Email,
        Notes = source.Notes,
        CreatedAtUtc = source.CreatedAtUtc,
        UpdatedAtUtc = source.UpdatedAtUtc
    };

    private static Quote CloneQuote(Quote source) => new()
    {
        Id = source.Id,
        Number = source.Number,
        ClientId = source.ClientId,
        ClientName = source.ClientName,
        Title = source.Title,
        Description = source.Description,
        Items = source.Items.Select(x => new QuoteItem
        {
            Description = x.Description,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice
        }).ToList(),
        Discount = source.Discount,
        Notes = source.Notes,
        Status = source.Status,
        ValidUntil = source.ValidUntil,
        CreatedAtUtc = source.CreatedAtUtc,
        UpdatedAtUtc = source.UpdatedAtUtc
    };

    private static BusinessProfile CloneProfile(BusinessProfile source) => new()
    {
        BusinessName = source.BusinessName,
        OwnerName = source.OwnerName,
        Document = source.Document,
        Phone = source.Phone,
        Email = source.Email,
        City = source.City,
        DefaultValidityDays = source.DefaultValidityDays,
        DefaultLaborValue = source.DefaultLaborValue
    };

    private async Task PersistUnsafeAsync(DataEnvelope data, string filePath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temp = $"{filePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = File.Create(temp))
                await JsonSerializer.SerializeAsync(stream, data, _jsonOptions, cancellationToken);

            File.Move(temp, filePath, true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { }
            }
        }
    }

    private sealed class DataEnvelope
    {
        public List<Client> Clients { get; set; } = [];
        public List<Quote> Quotes { get; set; } = [];
        public BusinessProfile BusinessProfile { get; set; } = new();
    }
}
