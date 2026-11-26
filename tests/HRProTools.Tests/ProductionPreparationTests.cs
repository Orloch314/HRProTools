using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using HRProTools.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRProTools.Tests;

public sealed class ProductionPreparationTests
{
    private sealed class Hosting : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "HRProTools";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("stream: OK\0", true)]
    [InlineData("stream: synthetic FOUND\0", false)]
    [InlineData("stream: ERROR\0", false)]
    public async Task UnixClamAvStreamsAllChunksAndRejectsUnconfirmedResults(string response, bool clean)
    {
        var path = Path.Combine(Path.GetTempPath(), "hr-" + Guid.NewGuid().ToString("N") + ".sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path)); listener.Listen(1);
        var content = new byte[140_000]; Random.Shared.NextBytes(content);
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptAsync(); using var stream = new NetworkStream(socket);
            var command = new byte[10]; await stream.ReadExactlyAsync(command); Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(command));
            using var captured = new MemoryStream(); var size = new byte[4];
            while (true) { await stream.ReadExactlyAsync(size); var count = BinaryPrimitives.ReadInt32BigEndian(size); if (count == 0) break; var bytes = new byte[count]; await stream.ReadExactlyAsync(bytes); captured.Write(bytes); }
            Assert.Equal(content, captured.ToArray());
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        });
        try
        {
            var scanner = new ClamAvFileSecurityScanner(Options.Create(new ClamAvOptions { UnixSocketPath = path, Required = true }), new Hosting());
            if (clean) { var result = await scanner.ScanAsync(content); Assert.True(result.WasScanned); Assert.Equal(FileScanStatuses.Clean, result.Status); }
            else await Assert.ThrowsAsync<InvalidOperationException>(() => scanner.ScanAsync(content));
            await server;
        }
        finally { listener.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task ProductionAntivirusFailsClosedAndTcpRemainsSupported()
    {
        var env = new Hosting();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClamAvFileSecurityScanner(Options.Create(new ClamAvOptions()), env).ScanAsync(new byte[1]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClamAvFileSecurityScanner(Options.Create(new ClamAvOptions { UnixSocketPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".sock") }), env).ScanAsync(new byte[1]));
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () => { using var client = await listener.AcceptTcpClientAsync(); using var stream = client.GetStream(); var command = new byte[10]; await stream.ReadExactlyAsync(command); var size = new byte[4]; await stream.ReadExactlyAsync(size); var body = new byte[BinaryPrimitives.ReadInt32BigEndian(size)]; await stream.ReadExactlyAsync(body); await stream.ReadExactlyAsync(size); await stream.WriteAsync("stream: OK\0"u8.ToArray()); });
        Assert.True((await new ClamAvFileSecurityScanner(Options.Create(new ClamAvOptions { Host = "127.0.0.1", Port = port }), env).ScanAsync(new byte[4])).WasScanned);
        await server;
    }

    [Theory]
    [InlineData("127.0.0.1", "https")]
    [InlineData("::1", "https")]
    [InlineData("198.51.100.23", "http")]
    public async Task ForwardedHeadersTrustOnlyTheSingleLoopbackProxy(string proxy, string scheme)
    {
        var options = new ForwardedHeadersOptions(); ProductionHosting.ConfigureForwarding(options);
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.Parse(proxy); context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https"; context.Request.Headers["X-Forwarded-For"] = "203.0.113.10";
        context.Request.Host = new HostString("app.hrprotools.com"); context.Request.Headers["X-Forwarded-Host"] = "attacker.example";
        using var logs = LoggerFactory.Create(_ => { });
        await new ForwardedHeadersMiddleware(_ => Task.CompletedTask, logs, Options.Create(options)).Invoke(context);
        Assert.Equal(scheme, context.Request.Scheme); Assert.Equal("app.hrprotools.com", context.Request.Host.Host);
        Assert.Equal(scheme == "https" ? "203.0.113.10" : proxy, context.Connection.RemoteIpAddress!.ToString());
    }

    [Fact]
    public void DataProtectionSurvivesNewProviderAndRejectsReleaseLocalKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), "hr-dp-" + Guid.NewGuid().ToString("N")); var keyPath = Path.Combine(root, "keys");
        var env = new Hosting { ContentRootPath = Path.Combine(root, "release") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DataProtection:KeyPath"] = keyPath }).Build();
        Assert.Equal(keyPath, ProductionHosting.KeyDirectory(config, env));
        var one = DataProtectionProvider.Create(new DirectoryInfo(keyPath), x => x.SetApplicationName("HRProTools")).CreateProtector("HRProTools.SmtpPassword.v1");
        var encrypted = one.Protect("synthetic-test-value");
        var two = DataProtectionProvider.Create(new DirectoryInfo(keyPath), x => x.SetApplicationName("HRProTools")).CreateProtector("HRProTools.SmtpPassword.v1");
        Assert.Equal("synthetic-test-value", two.Unprotect(encrypted));
        foreach (var invalid in new[] { "storage/keys", Path.Combine(env.ContentRootPath, "keys") })
            Assert.Throws<InvalidOperationException>(() => ProductionHosting.KeyDirectory(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DataProtection:KeyPath"] = invalid }).Build(), env));
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(root));
        Assert.StartsWith("hr-dp-", Path.GetFileName(root));
        Directory.Delete(root, recursive:true); // Only this test's freshly-created random directory.
    }

    [Fact]
    public async Task InitialAdminCannotBypassPasswordChangeAndBootstrapCannotOverwriteUsers()
    {
        var connection = Environment.GetEnvironmentVariable("HRPROTOOLS_BOOTSTRAP_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var services = new ServiceCollection(); services.AddLogging(); services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(x => x.UseNpgsql(connection, p => p.MigrationsHistoryTable("__ef_migrations_history", "auth")));
        services.AddIdentityCore<ApplicationUser>(x => { x.Password.RequiredLength=12; }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>,InitialPasswordClaimsFactory>();
        await using var provider = services.BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); await db.Database.MigrateAsync();
        var email = "bootstrap-qa@example.test"; var initial = "Synthetic!" + Guid.NewGuid().ToString("N"); var replacement = "Replacement!" + Guid.NewGuid().ToString("N");
        await IdentitySeeder.BootstrapProductionAsync(scope.ServiceProvider,email,initial);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var user = (await users.FindByEmailAsync(email))!;
        Assert.True(user.MustChangePassword); Assert.Null(user.TenantId); Assert.True(await users.IsInRoleAsync(user,AppRoles.GlobalAdministrator));
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>(); var principal = await factory.CreateAsync(user);
        Assert.True(principal.HasClaim(InitialPasswordClaimsFactory.Claim,"true"));
        Assert.False(InitialPasswordClaimsFactory.Ready(new AuthorizationHandlerContext([],principal,null)));
        var passed = false; var gate = new InitialPasswordMiddleware(_ => { passed=true; return Task.CompletedTask; });
        foreach(var (method,path,status) in new[]{("GET","/admin/grupos",302),("POST","/admin/grupos",403),("POST","/_blazor/negotiate",403),("GET","/_blazor",403),("POST","/Account/Manage/LinkExternalLogin",403)})
        {
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.Id)],"stale-cookie")) };
            context.Request.Method=method;context.Request.Path=path;passed=false;await gate.InvokeAsync(context,db);Assert.False(passed);Assert.Equal(status,context.Response.StatusCode);
        }
        var allowed = new DefaultHttpContext { User=principal };allowed.Request.Method="GET";allowed.Request.Path=InitialPasswordMiddleware.ChangePath;passed=false;await gate.InvokeAsync(allowed,db);Assert.True(passed);
        var password = new InitialPasswordService(users,db);
        Assert.False((await password.ChangeAsync(user,initial,initial)).Succeeded);Assert.True(user.MustChangePassword);
        Assert.False((await password.ChangeAsync(user,"wrong",replacement)).Succeeded);Assert.True(user.MustChangePassword);
        Assert.True((await password.ChangeAsync(user,initial,replacement)).Succeeded);Assert.False(user.MustChangePassword);
        Assert.False(await users.CheckPasswordAsync(user,initial));Assert.True(await users.CheckPasswordAsync(user,replacement));
        Assert.False((await factory.CreateAsync(user)).HasClaim(InitialPasswordClaimsFactory.Claim,"true"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>IdentitySeeder.BootstrapProductionAsync(scope.ServiceProvider,email,initial));
        Assert.Equal(1,await db.Users.CountAsync());Assert.Equal(0,await db.Tenants.CountAsync());
        var smtpAttempt = await ProductionSmtpSetup.ReserveSingleTestAsync(db,user.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>ProductionSmtpSetup.ReserveSingleTestAsync(db,user.Id));
        Assert.Equal(1,await db.OperationLogs.CountAsync(x=>x.Action==ProductionSmtpSetup.TestAction));
        await db.OperationLogs.Where(x=>x.Id==smtpAttempt).ExecuteDeleteAsync(); // Reservation only: never contacts SMTP in tests.
        Assert.True((await users.DeleteAsync(user)).Succeeded); // Only the synthetic user created by this test.
    }
}
