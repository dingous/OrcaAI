using System.Text.Json;
using Microsoft.Maui.Storage;
using OrcaAI.Models;

namespace OrcaAI.Services;

public sealed class JsonOrcaDataStore : IOrcaDataStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string _filePath;
    private DataEnvelope? _cache;

    public JsonOrcaDataStore()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "orcaai-data.json");
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
            client.UpdatedAtUtc = DateTime.UtcNow;
            if (index >= 0)
                data.Clients[index] = CloneClient(client);
            else
                data.Clients.Add(CloneClient(client));
        }, cancellationToken);
    }

    public Task DeleteClientAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(data => data.Clients.RemoveAll(x => x.Id == id), cancellationToken);

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
        var now = DateTime.UtcNow;
        var quotesThisMonth = data.Quotes.Count(x => x.CreatedAtUtc.Year == now.Year && x.CreatedAtUtc.Month == now.Month);
        var pending = data.Quotes.Count(x => x.Status is QuoteStatus.Draft or QuoteStatus.Sent);
        var approved = data.Quotes.Where(x => x.Status is QuoteStatus.Approved or QuoteStatus.Completed).Sum(x => x.Total);
        return new DashboardStats(data.Clients.Count, quotesThisMonth, pending, approved);
    }

    private async Task<DataEnvelope> GetDataAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache is not null)
                return _cache;

            if (!File.Exists(_filePath))
            {
                _cache = new DataEnvelope();
                await PersistUnsafeAsync(_cache, cancellationToken);
                return _cache;
            }

            await using var stream = File.OpenRead(_filePath);
            _cache = await JsonSerializer.DeserializeAsync<DataEnvelope>(stream, _jsonOptions, cancellationToken)
                     ?? new DataEnvelope();
            return _cache;
        }
        catch (JsonException)
        {
            _cache = new DataEnvelope();
            await PersistUnsafeAsync(_cache, cancellationToken);
            return _cache;
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
            if (_cache is null)
            {
                if (File.Exists(_filePath))
                {
                    try
                    {
                        await using var stream = File.OpenRead(_filePath);
                        _cache = await JsonSerializer.DeserializeAsync<DataEnvelope>(stream, _jsonOptions, cancellationToken);
                    }
                    catch (JsonException)
                    {
                        _cache = null;
                    }
                }

                _cache ??= new DataEnvelope();
            }

            mutation(_cache);
            await PersistUnsafeAsync(_cache, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
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

    private async Task PersistUnsafeAsync(DataEnvelope data, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temp = _filePath + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, data, _jsonOptions, cancellationToken);

        File.Move(temp, _filePath, true);
    }

    private sealed class DataEnvelope
    {
        public List<Client> Clients { get; set; } = [];
        public List<Quote> Quotes { get; set; } = [];
        public BusinessProfile BusinessProfile { get; set; } = new();
    }
}
