using System.Globalization;
using System.Text;
using Microsoft.Maui.Storage;
using OrcaAI.Models;

namespace OrcaAI.Services;

public sealed class SimplePdfService : IPdfService
{
    private static readonly Encoding PdfEncoding = Encoding.Latin1;
    private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<string> GenerateQuoteAsync(Quote quote, BusinessProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lines = BuildLines(quote, profile);
        var pages = Paginate(lines, 43);
        var bytes = BuildPdf(pages);

        var safeNumber = string.Concat(quote.Number.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        if (string.IsNullOrWhiteSpace(safeNumber))
            safeNumber = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        var path = Path.Combine(FileSystem.CacheDirectory, $"orcamento-{safeNumber}.pdf");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }

    private static List<PdfLine> BuildLines(Quote quote, BusinessProfile profile)
    {
        var lines = new List<PdfLine>
        {
            new("ORÇAAI · ORÇAMENTO PROFISSIONAL", true, 16),
            new($"Nº {quote.Number}", true, 10),
            new(string.Empty),
            new(profile.BusinessName, true, 12)
        };

        AddOptional(lines, "Responsável: ", profile.OwnerName);
        AddOptional(lines, "Documento: ", profile.Document);
        AddOptional(lines, "Contato: ", string.Join(" · ", new[] { profile.Phone, profile.Email }.Where(x => !string.IsNullOrWhiteSpace(x))));
        AddOptional(lines, "Cidade: ", profile.City);

        lines.Add(new(string.Empty));
        AddWrapped(lines, $"CLIENTE: {(string.IsNullOrWhiteSpace(quote.ClientName) ? "Não informado" : quote.ClientName)}", 82, true);
        AddWrapped(lines, $"SERVIÇO: {quote.Title}", 82, true);
        lines.Add(new($"VALIDADE: {quote.ValidUntil:dd/MM/yyyy}"));
        lines.Add(new(string.Empty));

        AddWrapped(lines, "Descrição: " + quote.Description, 82);
        lines.Add(new(string.Empty));
        lines.Add(new("ITENS", true, 11));
        lines.Add(new("Qtd.   Descrição                                  Unitário        Total", true, 9));
        lines.Add(new(new string('-', 76)));

        foreach (var item in quote.Items)
        {
            var descriptions = Wrap(item.Description, 38).ToList();
            if (descriptions.Count == 0)
                descriptions.Add("Item");

            var unit = item.UnitPrice.ToString("C", CurrencyCulture);
            var total = item.Total.ToString("C", CurrencyCulture);

            lines.Add(new($"{item.Quantity,5:0.##}  {descriptions[0],-38} {unit,12} {total,12}", false, 9));

            foreach (var continuation in descriptions.Skip(1))
                lines.Add(new($"       {continuation,-38}", false, 9));
        }

        lines.Add(new(string.Empty));
        lines.Add(new($"Subtotal: {quote.Subtotal.ToString("C", CurrencyCulture)}", true, 10));
        if (quote.Discount > 0)
            lines.Add(new($"Desconto: {quote.Discount.ToString("C", CurrencyCulture)}"));
        lines.Add(new($"TOTAL: {quote.Total.ToString("C", CurrencyCulture)}", true, 13));
        lines.Add(new(string.Empty));

        if (!string.IsNullOrWhiteSpace(quote.Notes))
        {
            lines.Add(new("OBSERVAÇÕES", true, 11));
            AddWrapped(lines, quote.Notes, 82);
            lines.Add(new(string.Empty));
        }

        lines.Add(new("Gerado pelo OrçaAI", false, 8));
        return lines;
    }

    private static void AddOptional(List<PdfLine> lines, string prefix, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            AddWrapped(lines, prefix + value.Trim(), 82);
    }

    private static void AddWrapped(List<PdfLine> lines, string text, int width, bool bold = false)
    {
        foreach (var part in Wrap(text, width))
            lines.Add(new PdfLine(part, bold));
    }

    private static IEnumerable<string> Wrap(string? text, int width)
    {
        var remaining = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(remaining))
            yield break;

        while (remaining.Length > width)
        {
            var split = remaining.LastIndexOf(' ', width);
            if (split <= 0)
                split = width;

            yield return remaining[..split].Trim();
            remaining = remaining[split..].Trim();
        }

        if (remaining.Length > 0)
            yield return remaining;
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace('“', '"')
            .Replace('”', '"')
            .Replace('’', '\'')
            .Replace('•', '-');

        while (normalized.Contains("  ", StringComparison.Ordinal))
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);

        return PdfEncoding.GetString(PdfEncoding.GetBytes(normalized.Trim()));
    }

    private static List<List<PdfLine>> Paginate(List<PdfLine> lines, int pageSize)
    {
        var result = new List<List<PdfLine>>();
        for (var i = 0; i < lines.Count; i += pageSize)
            result.Add(lines.Skip(i).Take(pageSize).ToList());

        return result.Count == 0 ? [[]] : result;
    }

    private static byte[] BuildPdf(List<List<PdfLine>> pages)
    {
        var objects = new List<byte[]>();
        var pageCount = pages.Count;
        var regularFontObject = 3 + pageCount * 2;
        var boldFontObject = regularFontObject + 1;
        var infoObject = boldFontObject + 1;

        objects.Add(Bytes("<< /Type /Catalog /Pages 2 0 R >>"));

        var kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(i => $"{3 + i * 2} 0 R"));
        objects.Add(Bytes($"<< /Type /Pages /Kids [ {kids} ] /Count {pageCount} >>"));

        for (var i = 0; i < pageCount; i++)
        {
            var pageObject = 3 + i * 2;
            var contentObject = pageObject + 1;

            objects.Add(Bytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {regularFontObject} 0 R /F2 {boldFontObject} 0 R >> >> /Contents {contentObject} 0 R >>"));

            var content = BuildContentStream(pages[i], i + 1, pageCount);
            var contentBytes = Bytes(content);
            objects.Add(Concat(
                Bytes($"<< /Length {contentBytes.Length} >>\nstream\n"),
                contentBytes,
                Bytes("\nendstream")));
        }

        objects.Add(Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        objects.Add(Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));
        objects.Add(Bytes("<< /Title (Orcamento OrcaAI) /Creator (OrcaAI) >>"));

        using var ms = new MemoryStream();
        Write(ms, "%PDF-1.4\n%âãÏÓ\n");
        var offsets = new List<long> { 0 };

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write(ms, $"{i + 1} 0 obj\n");
            ms.Write(objects[i]);
            Write(ms, "\nendobj\n");
        }

        var xrefPosition = ms.Position;
        Write(ms, $"xref\n0 {objects.Count + 1}\n");
        Write(ms, "0000000000 65535 f \n");

        for (var i = 1; i < offsets.Count; i++)
            Write(ms, $"{offsets[i]:0000000000} 00000 n \n");

        Write(ms, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info {infoObject} 0 R >>\nstartxref\n{xrefPosition}\n%%EOF");
        return ms.ToArray();
    }

    private static string BuildContentStream(List<PdfLine> lines, int page, int totalPages)
    {
        var sb = new StringBuilder();
        var y = 800;

        foreach (var line in lines)
        {
            var fontName = line.Bold ? "F2" : "F1";
            var fontSize = Math.Clamp(line.FontSize, 8, 18);
            sb.Append($"BT /{fontName} {fontSize} Tf 50 {y} Td ({Escape(line.Text)}) Tj ET\n");
            y -= fontSize >= 13 ? 20 : 16;
        }

        sb.Append($"BT /F1 8 Tf 50 28 Td (Página {page} de {totalPages}) Tj ET\n");
        return sb.ToString();
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static byte[] Bytes(string text) => PdfEncoding.GetBytes(text);

    private static byte[] Concat(params byte[][] arrays)
    {
        var result = new byte[arrays.Sum(x => x.Length)];
        var offset = 0;

        foreach (var array in arrays)
        {
            Buffer.BlockCopy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }

        return result;
    }

    private static void Write(Stream stream, string text)
    {
        var bytes = Bytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private sealed record PdfLine(string Text, bool Bold = false, int FontSize = 10);
}
