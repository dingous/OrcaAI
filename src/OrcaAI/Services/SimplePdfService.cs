using System.Globalization;
using System.Text;
using Microsoft.Maui.Storage;
using OrcaAI.Models;

namespace OrcaAI.Services;

public sealed class SimplePdfService : IPdfService
{
    private static readonly Encoding PdfEncoding = Encoding.Latin1;

    public async Task<string> GenerateQuoteAsync(Quote quote, BusinessProfile profile, CancellationToken cancellationToken = default)
    {
        var lines = BuildLines(quote, profile);
        var pages = Paginate(lines, 45);
        var bytes = BuildPdf(pages);
        var safeNumber = string.Concat(quote.Number.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        var path = Path.Combine(FileSystem.CacheDirectory, $"orcamento-{safeNumber}.pdf");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }

    private static List<string> BuildLines(Quote quote, BusinessProfile profile)
    {
        var currency = CultureInfo.GetCultureInfo("pt-BR");
        var lines = new List<string>
        {
            "ORÇAAI - ORÇAMENTO PROFISSIONAL",
            $"Nº {quote.Number}",
            "",
            profile.BusinessName,
            JoinNonEmpty("Responsável: ", profile.OwnerName),
            JoinNonEmpty("Documento: ", profile.Document),
            JoinNonEmpty("Contato: ", string.Join(" - ", new[] { profile.Phone, profile.Email }.Where(x => !string.IsNullOrWhiteSpace(x)))),
            JoinNonEmpty("Cidade: ", profile.City),
            "",
            $"CLIENTE: {(string.IsNullOrWhiteSpace(quote.ClientName) ? "Não informado" : quote.ClientName)}",
            $"SERVIÇO: {quote.Title}",
            $"VALIDADE: {quote.ValidUntil:dd/MM/yyyy}",
            ""
        };

        lines.AddRange(Wrap("Descrição: " + quote.Description, 84));
        lines.Add("");
        lines.Add("ITENS");
        lines.Add("Qtd.   Descrição                                      Unitário        Total");
        lines.Add(new string('-', 78));

        foreach (var item in quote.Items)
        {
            var desc = item.Description.Length > 42 ? item.Description[..39] + "..." : item.Description;
            lines.Add($"{item.Quantity,5:0.##}  {desc,-44} {item.UnitPrice.ToString("C", currency),12} {item.Total.ToString("C", currency),12}");
        }

        lines.Add("");
        lines.Add($"Subtotal: {quote.Subtotal.ToString("C", currency)}");
        if (quote.Discount > 0)
            lines.Add($"Desconto: {quote.Discount.ToString("C", currency)}");
        lines.Add($"TOTAL: {quote.Total.ToString("C", currency)}");
        lines.Add("");

        if (!string.IsNullOrWhiteSpace(quote.Notes))
        {
            lines.Add("OBSERVAÇÕES");
            lines.AddRange(Wrap(quote.Notes, 84));
            lines.Add("");
        }

        lines.Add("Gerado pelo OrçaAI");
        return lines.Where(x => x is not null).ToList()!;
    }

    private static string JoinNonEmpty(string prefix, string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : prefix + value;

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var remaining = text.Trim();
        while (remaining.Length > width)
        {
            var split = remaining.LastIndexOf(' ', width);
            if (split <= 0) split = width;
            yield return remaining[..split].Trim();
            remaining = remaining[split..].Trim();
        }

        if (remaining.Length > 0)
            yield return remaining;
    }

    private static List<List<string>> Paginate(List<string> lines, int pageSize)
    {
        var result = new List<List<string>>();
        for (var i = 0; i < lines.Count; i += pageSize)
            result.Add(lines.Skip(i).Take(pageSize).ToList());
        return result.Count == 0 ? [[]] : result;
    }

    private static byte[] BuildPdf(List<List<string>> pages)
    {
        var objects = new List<byte[]>();
        var pageCount = pages.Count;
        var fontObject = 3 + pageCount * 2;
        var infoObject = fontObject + 1;

        objects.Add(Bytes($"<< /Type /Catalog /Pages 2 0 R >>"));

        var kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(i => $"{3 + i * 2} 0 R"));
        objects.Add(Bytes($"<< /Type /Pages /Kids [ {kids} ] /Count {pageCount} >>"));

        for (var i = 0; i < pageCount; i++)
        {
            var pageObject = 3 + i * 2;
            var contentObject = pageObject + 1;
            objects.Add(Bytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontObject} 0 R >> >> /Contents {contentObject} 0 R >>"));

            var content = BuildContentStream(pages[i], i + 1, pageCount);
            var contentBytes = Bytes(content);
            objects.Add(Concat(Bytes($"<< /Length {contentBytes.Length} >>\nstream\n"), contentBytes, Bytes("\nendstream")));
        }

        objects.Add(Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        objects.Add(Bytes("<< /Title (Orcamento OrçaAI) /Creator (OrçaAI) >>"));

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

    private static string BuildContentStream(List<string> lines, int page, int totalPages)
    {
        var sb = new StringBuilder();
        var y = 800;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var font = i == 0 && page == 1 ? 16 : 10;
            sb.Append($"BT /F1 {font} Tf 50 {y} Td ({Escape(line)}) Tj ET\n");
            y -= 16;
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
}
