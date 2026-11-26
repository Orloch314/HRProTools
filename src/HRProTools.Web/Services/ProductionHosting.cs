using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace HRProTools.Web.Services;

public static class ProductionHosting
{
    public static void ConfigureForwarding(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
    }
    public static string KeyDirectory(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["DataProtection:KeyPath"];
        if (environment.IsProduction() && (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured)))
            throw new InvalidOperationException("Production requiere una ruta absoluta y persistente para Data Protection.");
        var path = Path.GetFullPath(configured ?? "storage/keys", environment.ContentRootPath);
        var relative = Path.GetRelativePath(Path.GetFullPath(environment.ContentRootPath), path);
        if (environment.IsProduction() && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathFullyQualified(relative))
            throw new InvalidOperationException("Data Protection debe estar fuera del directorio de publicación.");
        if (OperatingSystem.IsLinux())
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            if (environment.IsProduction() && (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                throw new InvalidOperationException("El directorio Data Protection debe tener permisos 0700.");
        }
        else Directory.CreateDirectory(path);
        return path;
    }
}
