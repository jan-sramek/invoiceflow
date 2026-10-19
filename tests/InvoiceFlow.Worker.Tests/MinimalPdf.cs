using System.Globalization;
using System.Text;

namespace InvoiceFlow.Worker.Tests;

/// <summary>
/// Builds tiny valid PDFs with correct xref offsets for PdfPig tests.
/// </summary>
internal static class MinimalPdf
{
    public static byte[] WithText(string text) => Build([text]);

    public static byte[] EmptyPage() => Build([""]);

    public static byte[] TwoPages(string page1, string page2) => Build([page1, page2]);

    private static byte[] Build(string[] pageTexts)
    {
        var objects = new List<byte[]>();
        void AddObject(string body) => objects.Add(Encoding.ASCII.GetBytes(body));

        int pagesId = 2;
        int firstPageId = 3;
        int firstContentId = 3 + pageTexts.Length;
        int fontId = firstContentId + pageTexts.Length;

        AddObject("1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n");

        var kids = new StringBuilder();
        for (int i = 0; i < pageTexts.Length; i++)
        {
            if (i > 0)
            {
                kids.Append(' ');
            }

            kids.Append(CultureInfo.InvariantCulture, $"{firstPageId + i} 0 R");
        }

        AddObject(
            string.Create(CultureInfo.InvariantCulture, $"2 0 obj<< /Type /Pages /Kids [{kids}] /Count {pageTexts.Length} >>endobj\n"));

        for (int i = 0; i < pageTexts.Length; i++)
        {
            int pageId = firstPageId + i;
            int contentId = firstContentId + i;
            AddObject(string.Create(
                CultureInfo.InvariantCulture,
                $"{pageId} 0 obj<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 300 144] " +
                $"/Contents {contentId} 0 R /Resources<< /Font<< /F1 {fontId} 0 R >> >> >>endobj\n"));
        }

        for (int i = 0; i < pageTexts.Length; i++)
        {
            int contentId = firstContentId + i;
            string stream = PageContent(pageTexts[i]);
            AddObject(string.Create(
                CultureInfo.InvariantCulture,
                $"{contentId} 0 obj<< /Length {stream.Length} >>stream\n{stream}\nendstream\nendobj\n"));
        }

        AddObject(string.Create(
            CultureInfo.InvariantCulture,
            $"{fontId} 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n"));

        var pdf = new MemoryStream();
        byte[] header = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        pdf.Write(header);

        var offsets = new List<long> { 0 };
        foreach (byte[] obj in objects)
        {
            offsets.Add(pdf.Position);
            pdf.Write(obj);
        }

        long xrefPosition = pdf.Position;
        var xref = new StringBuilder();
        xref.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n");
        xref.Append("0000000000 65535 f \n");
        for (int i = 1; i < offsets.Count; i++)
        {
            xref.Append(CultureInfo.InvariantCulture, $"{offsets[i]:D10} 00000 n \n");
        }

        xref.Append(CultureInfo.InvariantCulture, $"trailer<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        xref.Append(CultureInfo.InvariantCulture, $"startxref\n{xrefPosition}\n%%EOF\n");
        pdf.Write(Encoding.ASCII.GetBytes(xref.ToString()));
        return pdf.ToArray();
    }

    private static string PageContent(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "BT ET";
        }

        string escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
        return string.Create(CultureInfo.InvariantCulture, $"BT /F1 12 Tf 50 100 Td ({escaped}) Tj ET");
    }
}
