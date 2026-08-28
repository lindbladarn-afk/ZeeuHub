// Configures the portal host, security pipeline, integrations, and observability.
using WebApp.Seeding;
using AspNetCoreHero.ToastNotification.Extensions;
using Repository.Mapping;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.Extensions.Options;
using NLog;
using System.IO;
using System.Threading.RateLimiting;
using WebApp.Data;
using WebApp.Models.Identity;
using WebApp.Models.Application;
using WebApp.Resources.DataAnnotations;
using WebApp.Services.Application;
using WebApp.Services.Application.Infrastructure;
using WebApp.Services.Application.AI;
using WebApp.Services.Application.AI.Billing;
using WebApp.Services.Application.AI.Quota;
using WebApp.Services.Authentication.Infrastructure;
using WebApp.Services.Authorization.Infrastructure;
using WebApp.Services.Integration.Infrastructure;
using WebApp.Middleware;
using WebApp.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai-query", httpContext =>
        RateLimitPartition.GetConcurrencyLimiter(
            partitionKey: httpContext.User.Identity?.Name ??
                          httpContext.Connection.RemoteIpAddress?.ToString() ??
                          "anonymous",
            factory: _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = 2,
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

// =====================================================
// NLog
// =====================================================
LogManager.Setup().LoadConfigurationFromFile(
    Path.Combine(Directory.GetCurrentDirectory(), "nlog.config"));

// =====================================================
// Logging (Azure + docker)
// =====================================================
builder.Logging.AddAzureWebAppDiagnostics();
builder.Services.AddPortalObservability(builder.Configuration);

builder.Services.Configure<AzureFileLoggerOptions>(options =>
{
    options.FileName = "logs-";
    options.FileSizeLimit = 50 * 1024;
    options.RetainedFileCountLimit = 5;
});

builder.Services.Configure<AzureBlobLoggerOptions>(options =>
{
    options.BlobName = "log.txt";
});

// =====================================================
// Database – Identity
// =====================================================
var identityConnectionString =
    builder.Configuration.GetConnectionString("PortalIdentity")
    ?? Environment.GetEnvironmentVariable("CONNECTION_STRING_PORTAL_IDENTITY");

if (string.IsNullOrWhiteSpace(identityConnectionString))
{
    throw new Exception(
        "Missing connection string. Define ConnectionStrings:PortalIdentity " +
        "or set env var CONNECTION_STRING_PORTAL_IDENTITY.");
}

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
        options.UseSqlServer(
            identityConnectionString,
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null)),
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Singleton);

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(
        identityConnectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// =====================================================
// Identity
// =====================================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultUI()
.AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromDays(7);
});

// =====================================================
// HttpContext
// =====================================================
// (du hade redan singleton, men AddHttpContextAccessor är standard och safe)
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// =====================================================
// OpenAI options
// =====================================================
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<AiQuotaOptions>(builder.Configuration.GetSection("Ai:Quota"));
builder.Services.Configure<TechnicalNotificationOptions>(builder.Configuration.GetSection(TechnicalNotificationOptions.SectionName));
builder.Services.Configure<DataRetentionOptions>(builder.Configuration.GetSection(DataRetentionOptions.SectionName));

// =====================================================
// OPENAI (Azure OpenAI)
// =====================================================
builder.Services.AddHttpClient("OpenAI", (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;

    // Se till att Endpoint är en ren bas-url (utan /openai/v1)
    var endpoint = (options.Endpoint ?? "").Trim().TrimEnd('/');
    if (endpoint.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        endpoint = endpoint[..^("/openai/v1".Length)];
    if (endpoint.EndsWith("/openai/v1/", StringComparison.OrdinalIgnoreCase))
        endpoint = endpoint[..^("/openai/v1/".Length)];

    client.BaseAddress = new Uri(endpoint + "/");
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddScoped<IOpenAiChatService, OpenAiChatService>();

// =====================================================
// AI (DB-chat orchestration)  ✅ SAKNADES/behövde vara korrekt registrerat
// =====================================================
builder.Services.AddScoped<IAiSqlExecutor, AiSqlExecutor>();
builder.Services.AddScoped<IAiDataSourceResolver, AiDataSourceResolver>();
builder.Services.AddScoped<IAiRequestContextPolicy, AiRequestContextPolicy>();
builder.Services.AddSingleton<IAiSemanticCatalog, AiSemanticCatalog>();
builder.Services.AddSingleton<IAiSqlSecurityPolicy, AiSqlSecurityPolicy>();
builder.Services.AddSingleton<IAiResultVerifier, AiResultVerifier>();
builder.Services.AddSingleton<IAiPromptDataPolicy, AiPromptDataPolicy>();
builder.Services.AddScoped<IAiInvoiceQuestionService, AiInvoiceQuestionService>();
builder.Services.AddScoped<IAiDbChatOrchestrator, AiDbChatOrchestrator>();
builder.Services.AddSingleton<IAiConversationMemory, AiConversationMemory>();
builder.Services.AddScoped<IAiQuotaService, AiQuotaService>();
builder.Services.AddScoped<IAiQuotaAdminService, AiQuotaAdminService>();
builder.Services.AddScoped<IAiInvoiceExportService, AiInvoiceExportService>();

// =====================================================
// Dependency Injection – Core
// =====================================================
ApplicationServiceExtensions.ConfigureLoggerService(builder.Services);
AuthenticationServiceExtensions.ConfigureIdentitySettings(builder.Services, builder.Configuration);

AuthenticationServiceExtensions.AddAuthenticationCookie(builder.Services);
AuthenticationServiceExtensions.AddHubApiAuthentication(builder.Services, builder.Configuration);
AuthenticationServiceExtensions.ConfigureApplicationCookie(builder.Services, builder.Configuration);

builder.Services.AddPortalApplicationServices(builder.Configuration);

// =====================================================
// UI / UX
// =====================================================
ApplicationServiceExtensions.AddNotificationService(builder.Services);
ApplicationServiceExtensions.ConfigureLocalization(builder.Services);

// =====================================================
// MVC + Localization
// =====================================================
builder.Services.AddMvc()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider =
            (type, factory) => factory.Create(typeof(DataAnnotationsResource));
    });

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// =====================================================
// Session
// =====================================================
ApplicationServiceExtensions.AddSession(builder.Services, builder.Configuration);
builder.Services.AddMemoryCache();

// =====================================================
// Authorization
// =====================================================
AuthorizationServiceExtensions.AddAuthorizationPolicies(builder.Services, builder.Configuration);

// =====================================================
// Dapper mappings
// =====================================================
ConfigureMapping.ConfigureDapperMappings();

// =====================================================
// Form options (large uploads)
// =====================================================
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = int.MaxValue;
});

// =====================================================
// Data Protection (persist keys in docker volume)
// =====================================================
var dataProtectionKeysPath =
    builder.Configuration["DataProtection:KeysPath"]
    ?? Environment.GetEnvironmentVariable("DP_KEYS_PATH");

var defaultKeysDir =
    new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys"));

var keysDir = string.IsNullOrWhiteSpace(dataProtectionKeysPath)
    ? defaultKeysDir
    : new DirectoryInfo(dataProtectionKeysPath);

// macOS har ofta read-only root, så "/keys" kraschar lokalt.
if (OperatingSystem.IsMacOS() && string.Equals(keysDir.FullName, "/keys", StringComparison.OrdinalIgnoreCase))
{
    keysDir = defaultKeysDir;
}

Directory.CreateDirectory(keysDir.FullName);

builder.Services.AddDataProtection()
    .SetApplicationName("ZeeuCustomerPortal")
    .PersistKeysToFileSystem(keysDir);

// =====================================================
// BUILD
// =====================================================
var app = builder.Build();

// =====================================================
// Optional one-time menu seeding (set SEED_MENU=true)
// =====================================================
if (string.Equals(builder.Configuration["SEED_MENU"], "true", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        await MenuAutoSeeder.SeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "MenuAutoSeeder failed.");
    }
}

// =====================================================
// Pipeline
// =====================================================
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseRequestLocalization(
    app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.UseNotyf();
app.UseStaticFiles();

app.UseRouting();

// ⚠️ Session före auth om filters använder session
app.UseSession();

app.UseAuthentication();
app.UseMiddleware<RequestObservabilityMiddleware>();
app.UseMiddleware<UserSessionBootstrapMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

// Portal-sessiontelemetri (logga in/aktivitet) – efter auth så vi har User
app.UseMiddleware<PortalSessionMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Member}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
