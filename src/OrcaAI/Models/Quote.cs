using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;

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
    public decimal Subtotal
    {
        get
        {
            var total = 0m;
            foreach (var item in Items)
            {
                var value = item?.Total ?? 0m;
                if (value <= 0)
                    continue;

                if (total > decimal.MaxValue - value)
                    return decimal.MaxValue;

                total += value;
            }

            return total;
        }
    }

    [JsonIgnore]
    public decimal Total
    {
        get
        {
            var subtotal = Subtotal;
            var discount = Math.Max(0, Discount);
            return discount >= subtotal ? 0 : subtotal - discount;
        }
    }

    [JsonIgnore]
    public string ClientDisplayName =>
        string.IsNullOrWhiteSpace(ClientName) ? "Sem cliente" : ClientName;

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

    [JsonIgnore]
    public Color StatusBackgroundColor => Status switch
    {
        QuoteStatus.Draft => Color.FromArgb("#F1F2F6"),
        QuoteStatus.Sent => Color.FromArgb("#EFEDFF"),
        QuoteStatus.Approved => Color.FromArgb("#E8F8F2"),
        QuoteStatus.Rejected => Color.FromArgb("#FFF0F2"),
        QuoteStatus.Completed => Color.FromArgb("#E8F8F2"),
        _ => Color.FromArgb("#F1F2F6")
    };

    [JsonIgnore]
    public Color StatusTextColor => Status switch
    {
        QuoteStatus.Draft => Color.FromArgb("#6C7085"),
        QuoteStatus.Sent => Color.FromArgb("#4338B8"),
        QuoteStatus.Approved => Color.FromArgb("#087A59"),
        QuoteStatus.Rejected => Color.FromArgb("#C93C4A"),
        QuoteStatus.Completed => Color.FromArgb("#087A59"),
        _ => Color.FromArgb("#6C7085")
    };
}
