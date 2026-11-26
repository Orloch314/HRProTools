using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HRProTools.Web.Components;
using HRProTools.Web.Components.Account;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using HRProTools.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json;

var migrateOnly = args.Contains("--migrate-only");
var bootstrapOnly = args.Contains("--bootstrap-admin-stdin");
var smtpOnly = args.Contains("--configure-smtp-stdin");
args = args.Where(x => x is not "--migrate-only" and not "--bootstrap-admin-stdin" and not "--configure-smtp-stdin").ToArray();
var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsProduction() && !string.IsNullOrEmpty(builder.Configuration["BootstrapAdmin:Password"]))
    throw new InvalidOperationException("Production no admite BootstrapAdmin:Password. Utilice el bootstrap de entrada estándar con una nueva credencial.");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHealthChecks();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__ef_migrations_history", "auth")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddErrorDescriber<SpanishIdentityErrorDescriber>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, InitialPasswordClaimsFactory>();
builder.Services.AddScoped<InitialPasswordService>();
builder.Services.Configure<ForwardedHeadersOptions>(ProductionHosting.ConfigureForwarding);
if (builder.Environment.IsProduction())
{
    builder.Services.ConfigureApplicationCookie(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
    builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
}

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(24));
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppPolicies.GlobalAdministration, policy => policy.RequireRole(AppRoles.GlobalAdministrator).RequireAssertion(InitialPasswordClaimsFactory.Ready))
    .AddPolicy(AppPolicies.TenantAdministration, policy => policy.RequireRole(AppRoles.TenantAdministrator).RequireAssertion(InitialPasswordClaimsFactory.Ready))
    .AddPolicy(AppPolicies.EmployeeRecords, policy => policy.RequireRole(AppRoles.TenantAdministrator, AppRoles.HumanResources).RequireAssertion(InitialPasswordClaimsFactory.Ready))
    .AddPolicy(AppPolicies.SalaryRecords, policy => policy.RequireRole(AppRoles.TenantAdministrator, AppRoles.HumanResources).RequireAssertion(InitialPasswordClaimsFactory.Ready))
    .AddPolicy(AppPolicies.EmployeeSelfService, policy => policy.RequireRole(AppRoles.Employee).RequireAssertion(InitialPasswordClaimsFactory.Ready))
    .AddPolicy(AppPolicies.VacationWorkflowAccess, policy =>
        policy.RequireRole(AppRoles.TenantAdministrator, AppRoles.Approver, AppRoles.HumanResources).RequireAssertion(InitialPasswordClaimsFactory.Ready))
    .AddPolicy(AppPolicies.HumanResourcesOperations, policy => policy.RequireRole(AppRoles.HumanResources).RequireAssertion(InitialPasswordClaimsFactory.Ready));

var keyPath = ProductionHosting.KeyDirectory(builder.Configuration, builder.Environment);
builder.Services.AddDataProtection()
    .SetApplicationName("HRProTools")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath));

builder.Services.AddScoped<SmtpSettingsService>();
builder.Services.AddScoped<IEmailSender<ApplicationUser>, ApplicationEmailSender>();
builder.Services.AddScoped<CurrentTenantService>();
builder.Services.AddSingleton<TenantDbContextFactory>();
builder.Services.AddScoped<TenantProvisioningService>();
builder.Services.AddScoped<TenantDataService>();
builder.Services.AddScoped<InvitationAcceptanceService>();
builder.Services.Configure<ClamAvOptions>(builder.Configuration.GetSection("Antivirus:ClamAv"));
builder.Services.AddSingleton<IFileSecurityScanner, ClamAvFileSecurityScanner>();
builder.Services.AddScoped<TenantBrandingService>();
builder.Services.AddScoped<EmployeeAccessService>();
builder.Services.AddScoped<WorkCalendarService>();
builder.Services.AddScoped<CompanyDayOffService>();
builder.Services.AddScoped<VacationService>();
builder.Services.AddScoped<VacationWorkflowService>();
builder.Services.AddScoped<VacationRequestService>();
builder.Services.AddScoped<PermissionRequestService>();
builder.Services.AddScoped<PersonSexService>();
builder.Services.AddScoped<LegalPermissionReviewService>();
builder.Services.AddScoped<ReferenceAdministrationService>();
builder.Services.AddScoped<TenantCalendarService>();
builder.Services.AddScoped<AbsenceService>();
builder.Services.AddScoped<OperationalAlertService>();
builder.Services.AddScoped<RoleDashboardService>();
builder.Services.AddScoped<PermissionReportService>();
builder.Services.AddScoped<NotificationOutboxService>();
builder.Services.AddScoped<INotificationEmailTransport, SmtpNotificationEmailTransport>();
builder.Services.AddScoped<EmployeeExcelImportService>();
builder.Services.AddHostedService<VacationAccrualHostedService>();
builder.Services.AddHostedService<NotificationOutboxHostedService>();
builder.Services.AddHostedService<OperationalAlertHostedService>();

var app = builder.Build();

if (migrateOnly || builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, builder.Configuration, rolesOnly: builder.Environment.IsProduction());

    var tenantFactory = scope.ServiceProvider.GetRequiredService<TenantDbContextFactory>();
    var tenantSchemas = await db.Tenants.AsNoTracking()
        .Where(x => x.Status == TenantStatuses.Active)
        .Select(x => x.SchemaName)
        .ToListAsync();
    foreach (var schemaName in tenantSchemas)
    {
        await using var tenantDb = tenantFactory.Create(schemaName);
        await tenantDb.Database.MigrateAsync();
    }
}
if (migrateOnly) { Console.WriteLine("Migraciones HR ProTools aplicadas; no se iniciaron servidor ni workers."); return; }
if (bootstrapOnly || smtpOnly)
{
    if (!builder.Environment.IsProduction() || !Console.IsInputRedirected)
        throw new InvalidOperationException("El bootstrap requiere Production y entrada estándar protegida desde el wrapper interactivo.");
    try
    {
        var input = new char[4097]; var count = 0;
        while (count < input.Length) { var character = Console.Read(); if (character is -1 or 10) break; input[count++] = (char)character; }
        if (count > 4096) throw new InvalidOperationException();
        using var json = JsonDocument.Parse(new string(input, 0, count));
        Array.Clear(input);
        var password = json.RootElement.GetProperty("password").GetString() ?? "";
        await using var scope = app.Services.CreateAsyncScope();
        if (bootstrapOnly)
        {
            await IdentitySeeder.BootstrapProductionAsync(scope.ServiceProvider, builder.Configuration["BootstrapAdmin:Email"] ?? "", password);
            Console.WriteLine("Administrador inicial creado; cambio de contraseña obligatorio. No se iniciaron workers.");
        }
        else await ProductionSmtpSetup.ConfigureAndTestAsync(scope.ServiceProvider, password);
    }
    catch (Exception) { Console.Error.WriteLine("No se completó la configuración protegida. Compruebe su estado; SMTP no se reenvía automáticamente. No se registró la credencial."); Environment.ExitCode = 1; }
    return;
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseMiddleware<InitialPasswordMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");
app.MapTenantLogo();
app.MapPhase5Endpoints();
app.MapGet("/tenant/importacion/plantilla", () => Results.File(
        EmployeeExcelImportService.CreateTemplate(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Plantilla-importacion-HR-ProTools.xlsx"))
    .RequireAuthorization(AppPolicies.TenantAdministration);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
