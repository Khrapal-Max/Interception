//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Services;
using Interception.UI.Application.Database.Abstractions;
using Interception.UI.Application.Database.Services;
using Interception.UI.Application.Exports.Abstractions;
using Interception.UI.Application.Exports.Services;
using Interception.UI.Application.Import.Abstractions;
using Interception.UI.Application.Import.Services;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Services;
using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Services;
using Interception.UI.Application.Reports.Abstractions;
using Interception.UI.Application.Reports.Services;
using Interception.UI.Application.Toasts;
using Interception.UI.Components;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Razor + Blazor Server (Interactive)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(opt => { opt.DetailedErrors = true; });

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "data");
var keysDir = Path.Combine(dataDir, "keys");
var dbPath = Path.Combine(dataDir, "interception.db");

Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(keysDir);

var sqliteConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(sqliteConnection))
    sqliteConnection = $"Data Source={dbPath}";

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseSqlite(sqliteConnection);
});

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir));

builder.Services.AddScoped<ToastService>();

// --- Application services ---
// Поточні контракти, які ще використовує фронт.
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();
builder.Services.AddScoped<IInterceptionImportService, InterceptionImportService>();
builder.Services.AddScoped<IInterceptionActionService, InterceptionActionService>();
builder.Services.AddScoped<IPersonRegistryService, PersonRegistryService>();

// Нові registry-сервіси 
builder.Services.AddScoped<IInterceptionQueryService, InterceptionQueryService>();
builder.Services.AddScoped<IInterceptionCommandService, InterceptionCommandService>();
builder.Services.AddScoped<IInterceptionSuggestionService, InterceptionSuggestionService>();
builder.Services.AddScoped<IFrequencyDivisionService, FrequencyDivisionService>();

// Нові candidate-сервіси
builder.Services.AddScoped<ITopologySnapshotBuilder, TopologySnapshotBuilder>();
builder.Services.AddScoped<ILinkMapService, LinkMapService>();
builder.Services.AddScoped<IGroupHierarchyService, GroupHierarchyService>();
builder.Services.AddScoped<IContextSuggestionService, ContextSuggestionService>();
builder.Services.AddScoped<IFrequencyWeightReportService, FrequencyWeightReportService>();
builder.Services.AddScoped<IKnownParticipantSuggestionService, KnownParticipantSuggestionService>();
builder.Services.AddScoped<IParticipantCandidateAnalysisService, ParticipantCandidateAnalysisService>();
builder.Services.AddScoped<IParticipantCandidateGroupQueryService, ParticipantCandidateGroupQueryService>();
builder.Services.AddScoped<IParticipantCandidateGroupCommandService, ParticipantCandidateGroupCommandService>();

// Нові reports-сервіси 
builder.Services.AddScoped<IDayPictureService, DayPictureService>();
builder.Services.AddScoped<IDivisionReportService, DivisionReportService>();

// Portable database tools
builder.Services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/api/database/export", async (IDatabaseMaintenanceService dbService, CancellationToken ct) =>
{
    var export = await dbService.PrepareExportAsync(ct);
    return Results.File(export.FilePath, "application/octet-stream", export.DownloadFileName);
});

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.ContentSecurityPolicy =
        "frame-ancestors 'none'";
    await next();
});

await app.AddMigrationDb();
app.Run();
