namespace HRProTools.Web.Services;

public sealed record ValidatedLogo(string ContentType, int Size);

public static class TenantLogoFileValidator
{
    public const int MaximumBytes = 2 * 1024 * 1024;

    public static ValidatedLogo Validate(ReadOnlySpan<byte> content)
    {
        if (content.Length == 0)
            throw new InvalidOperationException("Seleccione una imagen valida.");
        if (content.Length > MaximumBytes)
            throw new InvalidOperationException("El logo no puede superar 2 MB.");

        var contentType = DetectContentType(content)
            ?? throw new InvalidOperationException("El contenido no corresponde a una imagen PNG, JPEG o WebP valida.");
        return new ValidatedLogo(contentType, content.Length);
    }

    public static bool IsAllowedContentType(string value) =>
        value is "image/png" or "image/jpeg" or "image/webp";

    private static string? DetectContentType(ReadOnlySpan<byte> content)
    {
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (content.Length >= 16 && content[..8].SequenceEqual(png) && content.Slice(12, 4).SequenceEqual("IHDR"u8))
            return "image/png";

        if (content.Length >= 4 &&
            content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF &&
            content[^2] == 0xFF && content[^1] == 0xD9)
            return "image/jpeg";

        if (content.Length >= 16 &&
            content[..4].SequenceEqual("RIFF"u8) &&
            content.Slice(8, 4).SequenceEqual("WEBP"u8) &&
            (content.Slice(12, 4).SequenceEqual("VP8 "u8) ||
             content.Slice(12, 4).SequenceEqual("VP8L"u8) ||
             content.Slice(12, 4).SequenceEqual("VP8X"u8)))
            return "image/webp";

        return null;
    }
}
