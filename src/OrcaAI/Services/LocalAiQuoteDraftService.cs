using System.Globalization;
using System.Text.RegularExpressions;
using OrcaAI.Models;

namespace OrcaAI.Services;

public sealed partial class LocalAiQuoteDraftService : IAiQuoteDraftService
{
    private static readonly (string[] Keywords, string Title)[] Titles =
    [
        (["tomada", "disjuntor", "luminária", "eletric", "fiação"], "Serviço elétrico"),
        (["ar condicionado", "ar-condicionado", "split", "climat"], "Serviço de ar-condicionado"),
        (["pintura", "pintar", "tinta", "parede"], "Serviço de pintura"),
        (["encan", "torneira", "vazamento", "hidrául"], "Serviço hidráulico"),
        (["computador", "notebook", "rede", "wifi", "wi-fi"], "Assistência técnica"),
        (["jardim", "grama", "poda", "jardin"], "Serviço de jardinagem"),
        (["foto", "fotografia", "filmagem", "vídeo"], "Serviço audiovisual")
    ];

    public Task<QuoteDraft> CreateDraftAsync(string description, BusinessProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        description = Regex.Replace(description.Trim(), @"\s+", " ");
        var title = DetectTitle(description);
        var items = ExtractItems(description).ToList();

        if (items.Count == 0)
        {
            items.Add(new QuoteItem
            {
                Description = "Serviço conforme descrição",
                Quantity = 1,
                UnitPrice = 0
            });
        }

        if (profile.DefaultLaborValue > 0 && items.All(x => !x.Description.Contains("mão de obra", StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(new QuoteItem
            {
                Description = "Mão de obra",
                Quantity = 1,
                UnitPrice = profile.DefaultLaborValue
            });
        }

        return Task.FromResult(new QuoteDraft
        {
            Title = title,
            Description = description,
            Items = items
        });
    }

    private static string DetectTitle(string description)
    {
        var normalized = description.ToLowerInvariant();
        foreach (var (keywords, title) in Titles)
            if (keywords.Any(normalized.Contains))
                return title;

        var first = description.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
            return "Orçamento de serviço";

        return first.Length <= 48 ? Capitalize(first) : Capitalize(first[..48].TrimEnd()) + "…";
    }

    private static IEnumerable<QuoteItem> ExtractItems(string description)
    {
        var segments = Regex.Split(description, @"\s*(?:,|;|\be\b|\bmais\b)\s*", RegexOptions.IgnoreCase)
            .Select(x => x.Trim(' ', '.', ':', '-'))
            .Where(x => x.Length >= 3)
            .Take(8);

        foreach (var segment in segments)
        {
            var match = QuantityRegex().Match(segment);
            if (!match.Success)
                continue;

            var raw = match.Groups["qty"].Value.Replace(',', '.');
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
                quantity = 1;

            var text = segment.Remove(match.Index, match.Length).Trim();
            text = LeadingArticleRegex().Replace(text, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            yield return new QuoteItem
            {
                Description = Capitalize(text),
                Quantity = Math.Max(1, quantity),
                UnitPrice = 0
            };
        }
    }

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpper(value[0]) + value[1..];

    [GeneratedRegex(@"(?<qty>\d+(?:[\.,]\d+)?)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityRegex();

    [GeneratedRegex(@"^(?:de|do|da|dos|das|um|uma|uns|umas)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingArticleRegex();
}
