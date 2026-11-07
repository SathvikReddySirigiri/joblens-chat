using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace JobLensChat.Services;

public sealed record ExtractOutcome(bool Success, string Text, string? Error);

// Reads an upload in memory. The file is never written to disk.
public sealed class OfferTextExtractor
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public ExtractOutcome Extract(string? fileName, ReadOnlyMemory<byte> content)
    {
        if (content.Length == 0)
        {
            return Fail("Choose a PDF or Word file.");
        }

        if (content.Length > MaxBytes)
        {
            return Fail("The file must be 2 MB or smaller.");
        }

        var extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (extension is not ".pdf" and not ".docx")
        {
            return Fail("Upload a .pdf or .docx file.");
        }

        var bytes = content.Span;
        var pdf = bytes.Length >= 4
            && bytes[0] == (byte)'%'
            && bytes[1] == (byte)'P'
            && bytes[2] == (byte)'D'
            && bytes[3] == (byte)'F';
        var zip = bytes.Length >= 2 && bytes[0] == (byte)'P' && bytes[1] == (byte)'K';
        if (extension == ".pdf" && !pdf)
        {
            return Fail("The file contents do not match a PDF or Word document.");
        }

        if (extension == ".docx" && !zip)
        {
            return Fail("The file contents do not match a PDF or Word document.");
        }

        try
        {
            return extension == ".pdf" ? ReadPdf(content) : ReadDocx(content);
        }
        catch (Exception)
        {
            return Fail("The file could not be read. Please paste the text instead.");
        }
    }

    private static ExtractOutcome ReadPdf(ReadOnlyMemory<byte> content)
    {
        using var document = PdfDocument.Open(content.ToArray());
        if (document.NumberOfPages > 10)
        {
            return Fail("PDF files are limited to 10 pages.");
        }

        var text = string.Join('\n', document.GetPages().Select(page => page.Text));
        if (text.Trim().Length < 50)
        {
            return Fail("This looks like a scanned document. Please paste the text instead.");
        }

        return new ExtractOutcome(true, text, null);
    }

    private static ExtractOutcome ReadDocx(ReadOnlyMemory<byte> content)
    {
        using var stream = new MemoryStream(content.ToArray());
        using var document = WordprocessingDocument.Open(stream, false);
        var text = document.MainDocumentPart?.Document?.Body?.InnerText ?? "";
        if (text.Trim().Length < 50)
        {
            return Fail("This looks like a scanned document. Please paste the text instead.");
        }

        return new ExtractOutcome(true, text, null);
    }

    private static ExtractOutcome Fail(string error) => new(false, "", error);
}
