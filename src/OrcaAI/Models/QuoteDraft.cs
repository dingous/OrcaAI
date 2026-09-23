namespace OrcaAI.Models;

public sealed class QuoteDraft
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<QuoteItem> Items { get; init; } = [];
}
