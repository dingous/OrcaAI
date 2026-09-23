using OrcaAI.Models;

namespace OrcaAI.Services;

public interface IOrcaDataStore
{
    Task<IReadOnlyList<Client>> GetClientsAsync(CancellationToken cancellationToken = default);
    Task<Client?> GetClientAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveClientAsync(Client client, CancellationToken cancellationToken = default);
    Task DeleteClientAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Quote>> GetQuotesAsync(CancellationToken cancellationToken = default);
    Task<Quote?> GetQuoteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveQuoteAsync(Quote quote, CancellationToken cancellationToken = default);
    Task DeleteQuoteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BusinessProfile> GetBusinessProfileAsync(CancellationToken cancellationToken = default);
    Task SaveBusinessProfileAsync(BusinessProfile profile, CancellationToken cancellationToken = default);
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}
