using OrcaAI.Models;

namespace OrcaAI.Services;

public interface IPdfService
{
    Task<string> GenerateQuoteAsync(Quote quote, BusinessProfile profile, CancellationToken cancellationToken = default);
}
