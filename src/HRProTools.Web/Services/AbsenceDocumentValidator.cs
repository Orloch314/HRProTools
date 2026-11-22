namespace HRProTools.Web.Services;

public sealed record ValidatedAbsenceDocument(string ContentType, string Extension);

public static class AbsenceDocumentValidator
{
    public const int MaxSize = 5 * 1024 * 1024;

    public static ValidatedAbsenceDocument Validate(string fileName, string declaredContentType, byte[] content)
    {
        if (content.Length == 0 || content.Length > MaxSize)
            throw new InvalidOperationException("El archivo debe tener contenido y no superar 5 MB.");

        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        var actual = Detect(content);
        var allowed = actual switch
        {
            "application/pdf" => extension == ".pdf" && declaredContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase),
            "image/jpeg" => extension is ".jpg" or ".jpeg" && declaredContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase),
            "image/png" => extension == ".png" && declaredContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
        if (!allowed)
            throw new InvalidOperationException("La extension, el tipo MIME y la firma real del archivo no coinciden con PDF, JPEG o PNG.");
        return new ValidatedAbsenceDocument(actual, extension);
    }

    private static string Detect(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 5 && content[..5].SequenceEqual("%PDF-"u8)) return "application/pdf";
        if (content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff) return "image/jpeg";
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        if (content.Length >= png.Length && content[..png.Length].SequenceEqual(png)) return "image/png";
        return string.Empty;
    }
}
