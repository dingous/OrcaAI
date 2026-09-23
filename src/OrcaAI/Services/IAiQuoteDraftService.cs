using OrcaAI.Models;

namespace OrcaAI.Services;

public interface IAiQuoteDraftService
{
    Task<QuoteDraft> CreateDraftAsync(string description, BusinessProfile profile, CancellationToken cancellationToken = default);
}
