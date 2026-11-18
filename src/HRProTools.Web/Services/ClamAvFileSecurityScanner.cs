using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace HRProTools.Web.Services;

public sealed class ClamAvOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 15;
    public bool Required { get; set; }
}

public sealed record FileScanResult(string Status, DateTimeOffset? ScannedAtUtc, bool WasScanned);

public static class FileScanStatuses
{
    public const string Clean = "Clean";
    public const string DevelopmentUnscanned = "DevelopmentUnscanned";
}

public interface IFileSecurityScanner
{
    Task<FileScanResult> ScanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
}

public sealed class ClamAvFileSecurityScanner(
    IOptions<ClamAvOptions> options,
    IWebHostEnvironment environment) : IFileSecurityScanner
{
    public async Task<FileScanResult> ScanAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            if (environment.IsProduction() || settings.Required)
                throw new InvalidOperationException("El escaneo antivirus es obligatorio y ClamAV no esta configurado.");

            return new FileScanResult(FileScanStatuses.DevelopmentUnscanned, null, false);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 120)));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(settings.Host, settings.Port, timeout.Token);
            await using var stream = client.GetStream();

            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            const int chunkSize = 64 * 1024;
            var lengthBuffer = new byte[4];
            for (var offset = 0; offset < content.Length; offset += chunkSize)
            {
                var length = Math.Min(chunkSize, content.Length - offset);
                BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, length);
                await stream.WriteAsync(lengthBuffer, timeout.Token);
                await stream.WriteAsync(content.Slice(offset, length), timeout.Token);
            }
            Array.Clear(lengthBuffer);
            await stream.WriteAsync(lengthBuffer, timeout.Token);
            await stream.FlushAsync(timeout.Token);

            var responseBuffer = new byte[1024];
            var response = new StringBuilder();
            int read;
            do
            {
                read = await stream.ReadAsync(responseBuffer, timeout.Token);
                if (read > 0)
                    response.Append(Encoding.UTF8.GetString(responseBuffer, 0, read));
            }
            while (read > 0 && !response.ToString().Contains('\0'));

            var result = response.ToString().TrimEnd('\0', '\r', '\n');
            if (result.EndsWith(" OK", StringComparison.Ordinal))
                return new FileScanResult(FileScanStatuses.Clean, DateTimeOffset.UtcNow, true);
            if (result.Contains(" FOUND", StringComparison.Ordinal))
                throw new InvalidOperationException("El archivo fue rechazado por el antivirus.");

            throw new InvalidOperationException("ClamAV no pudo confirmar que el archivo sea seguro.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            throw new InvalidOperationException("No fue posible completar el escaneo antivirus. Intente nuevamente.", ex);
        }
    }
}
