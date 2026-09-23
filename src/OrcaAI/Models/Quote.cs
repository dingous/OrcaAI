using System.Text.Json.Serialization;

namespace OrcaAI.Models;

public sealed class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<QuoteItem> Items { get; set; } = [];
    public decimal Discount { get; set; }
    public string Notes { get; set; } = string.Empty;
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public DateTime ValidUntil { get; set; } = DateTime.Today.AddDays(7);
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public decimal Subtotal => Items.Sum(x => x.Total);

    [JsonIgnore]
    public decimal Total => Math.Max(0, Subtotal - Discount);

    [JsonIgnore]
    public string StatusLabel => Status switch
    {
        QuoteStatus.Draft => "Rascunho",
        QuoteStatus.Sent => "Enviado",
        QuoteStatus.Approved => "Aprovado",
        QuoteStatus.Rejected => "Recusado",
        QuoteStatus.Completed => "Concluído",
        _ => Status.ToString()
    };
}
