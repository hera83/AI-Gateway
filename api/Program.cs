using api.BgWorkers;
using api.Data;
using api.Data.Seeds;
using api.Infrastructure.Authentication;
using api.Infrastructure.Database;
using api.Infrastructure.ErrorHandling;
using api.Infrastructure.Logging;
using api.Infrastructure.OpenApi;
using api.Managers.ApiKeys;
using api.Managers.ApiKeys.Interfaces;
using api.Managers.KnowledgeBase;
using api.Managers.KnowledgeBase.Interfaces;
using api.Repositories.ApiKeys;
using api.Repositories.ApiKeys.Interfaces;
using api.Repositories.KnowledgeDocuments;
using api.Repositories.KnowledgeDocuments.Interfaces;
using api.Repositories.KnowledgeGroups;
using api.Repositories.KnowledgeGroups.Interfaces;
using api.Repositories.Logs;
using api.Repositories.Logs.Interfaces;
using api.Services.Ollama;
using api.Services.Ollama.Interfaces;
using api.Services.Speaches;
using api.Services.Speaches.Interfaces;
using api.Services.Tesseract;
using api.Services.Tesseract.Interfaces;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var apiName = builder.Configuration["Api:Name"] ?? "api";

var environmentName = builder.Environment.EnvironmentName;
var environmentBadgeColor = environmentName switch
{
    "Production" => "brightgreen",
    "Development" => "orange",
    _ => "yellow"
};
var environmentBadgeMarkdown =
    $"![{environmentName}](https://img.shields.io/badge/env-{Uri.EscapeDataString(environmentName)}-{environmentBadgeColor})";

// Serilog and the log database
var logDatabaseSettings = new LogDatabaseSettings(BuildDatabasePath(Path.Combine("app_dbs", "log.db")));
builder.Services.AddSingleton(logDatabaseSettings);
builder.Host.UseSerilog((_, services, loggerConfiguration) => loggerConfiguration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.SQLite(logDatabaseSettings.DatabasePath, logDatabaseSettings.TableName, storeTimestampInUtc: true, rollOver: false));

// Uploads (Speaches audio up to ~300 MB) need higher limits than Kestrel's 30 MB and the 128 MB multipart default.
// Kestrel does not bind Limits.* from configuration on its own, so the value is read explicitly.
var maxRequestBodySize = builder.Configuration.GetValue<long>("Kestrel:Limits:MaxRequestBodySize", 300_000_000);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBodySize);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxRequestBodySize);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>();
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = apiName;
        document.Info.Description = environmentBadgeMarkdown;
        document.Servers?.Clear();
        return Task.CompletedTask;
    });
});

builder.Services.AddHttpClient();

// vectors.db is attached to every app.db connection as "vectors" (sqlite-vec), see SqliteVecConnectionInterceptor.
var appConnectionString = BuildConnectionString("Default");
var vectorDatabasePath = new SqliteConnectionStringBuilder(BuildConnectionString("Vectors")).DataSource;
builder.Services.AddDbContext<ApiDbContext>(options => options
    .UseSqlite(appConnectionString)
    .AddInterceptors(new SqliteVecConnectionInterceptor(vectorDatabasePath)));

builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy(AuthorizationPolicies.MasterKeyOnly, policy =>
        policy.RequireClaim(ApiKeyClaimTypes.KeyType, ApiKeyTypes.Master));
    options.AddPolicy(AuthorizationPolicies.StandardKeyOnly, policy =>
        policy.RequireClaim(ApiKeyClaimTypes.KeyType, ApiKeyTypes.Standard));
});

// Repositories
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
builder.Services.AddScoped<ILogRepository, LogRepository>();
builder.Services.AddScoped<IKnowledgeGroupRepository, KnowledgeGroupRepository>();
builder.Services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();

// Managers
builder.Services.Configure<KnowledgeBaseSettings>(builder.Configuration.GetSection("KnowledgeBase"));
builder.Services.AddScoped<IKnowledgeBaseManager, KnowledgeBaseManager>();
builder.Services.AddScoped<IApiKeyManager, ApiKeyManager>();
builder.Services.AddSingleton<KnowledgeDocumentProcessingSignal>();

// External services
builder.Services.AddScoped<IOllamaConfigurationProvider, OllamaConfigurationProvider>();
builder.Services.AddScoped<OllamaHttpClientFactory>();
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<ISpeachesConfigurationProvider, SpeachesConfigurationProvider>();
builder.Services.AddScoped<SpeachesHttpClientFactory>();
builder.Services.AddScoped<ISpeachesService, SpeachesService>();
builder.Services.AddScoped<ITesseractService, TesseractService>();

// Workers
builder.Services.AddHostedService<LogCleanupWorker>();
builder.Services.AddHostedService<KnowledgeDocumentProcessingWorker>();
builder.Services.AddHostedService<KnowledgeMarkdownWorker>();
builder.Services.AddHostedService<KnowledgeFileCleanupWorker>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
    await dbContext.Database.MigrateAsync();
    await DataSeeder.SeedAsync(dbContext);
}

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" && HttpMethods.IsGet(context.Request.Method))
    {
        context.Response.Redirect("/swagger");
        return;
    }

    await next();
});

app.MapOpenApi().AllowAnonymous();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", apiName);
    options.DocumentTitle = apiName;
    options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    options.DefaultModelsExpandDepth(-1);
});

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();

// Required by /Speaches/TranscribeRealtime (WebSocket proxy).
app.UseWebSockets();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Relative "Data Source" values from appsettings are resolved against ContentRootPath (app_dbs/).
string BuildConnectionString(string name)
{
    var connectionString = new SqliteConnectionStringBuilder(
        builder.Configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' is missing."));
    connectionString.DataSource = BuildDatabasePath(connectionString.DataSource);
    return connectionString.ToString();
}

string BuildDatabasePath(string path)
{
    var fullPath = Path.Combine(builder.Environment.ContentRootPath, path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    return fullPath;
}
