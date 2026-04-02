//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Abstractions;
using Interception.Application.Analytics.Services;
using Interception.Application.Import.Abstractions;
using Interception.Application.Import.Services;
using Interception.Application.Interceptions.Abstractions;
using Interception.Application.Interceptions.Services;
using Interception.Application.Registry.Abstractions;
using Interception.Application.Registry.Services;
using Interception.Application.Reports.Abstractions;
using Interception.Application.Reports.Services;
using Interception.Application.Toasts;
using Interception.Infrastructure.PostgreSql;
using Interception.Web.UI.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Razor + Blazor Server (Interactive)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(opt => { opt.DetailedErrors = true; });

builder.Services.AddDbContextFactory<PostgreSqlDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")); // або ваш провайдер
});
builder.Services.AddDatabaseStartupMigration();

// DataProtection keys must survive restarts and be shared across instances
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/data-protection-keys"));

builder.Services.AddScoped<ToastService>();

// --- Application services ---
// Поточні контракти, які ще використовує фронт.
builder.Services.AddScoped<IInterceptionImportService, InterceptionImportService>();
builder.Services.AddScoped<IInterceptionActionService, InterceptionActionService>();
builder.Services.AddScoped<IPersonRegistryService, PersonRegistryService>();

// Нові registry-сервіси 
builder.Services.AddScoped<IInterceptionQueryService, InterceptionQueryService>();
builder.Services.AddScoped<IInterceptionCommandService, InterceptionCommandService>();
builder.Services.AddScoped<IInterceptionSuggestionService, InterceptionSuggestionService>();
builder.Services.AddScoped<IFrequencyDivisionService, FrequencyDivisionService>();

// Нові candidate-сервіси
builder.Services.AddScoped<ILinkMapService, LinkMapService>();
builder.Services.AddScoped<IContextSuggestionService, ContextSuggestionService>();
builder.Services.AddScoped<IKnownParticipantSuggestionService, KnownParticipantSuggestionService>();
builder.Services.AddScoped<IParticipantCandidateAnalysisService, ParticipantCandidateAnalysisService>();
builder.Services.AddScoped<IParticipantCandidateGroupQueryService, ParticipantCandidateGroupQueryService>();
builder.Services.AddScoped<IParticipantCandidateGroupCommandService, ParticipantCandidateGroupCommandService>();

// Нові reports-сервіси 
builder.Services.AddScoped<IDayPictureService, DayPictureService>();
builder.Services.AddScoped<IDivisionReportService, DivisionReportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.ContentSecurityPolicy =
        "frame-ancestors 'none'";
    await next();
});

app.Run();
