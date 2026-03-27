//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Candidates;
using Interception.UI.Application.Interceptions.Abstractions.Import;
using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Abstractions.Reports;
using Interception.UI.Application.Interceptions.Services.Candidates;
using Interception.UI.Application.Interceptions.Services.Import;
using Interception.UI.Application.Interceptions.Services.Registry;
using Interception.UI.Application.Interceptions.Services.Reports;
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

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")); // або ваш провайдер
});

// DataProtection keys must survive restarts and be shared across instances
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/data-protection-keys"));

builder.Services.AddScoped<ToastService>();

// --- Application services ---
// Поточні контракти, які ще використовує фронт.
builder.Services.AddScoped<IInterceptionImportService, InterceptionImportService>();
builder.Services.AddScoped<IInterceptionActionService, InterceptionActionService>();
builder.Services.AddScoped<IResolvedParticipantService, ResolvedParticipantService>();

// Нові registry-сервіси 
builder.Services.AddScoped<IInterceptionQueryService, InterceptionQueryService>();
builder.Services.AddScoped<IInterceptionCommandService, InterceptionCommandService>();
builder.Services.AddScoped<IInterceptionSuggestionService, InterceptionSuggestionService>();
builder.Services.AddScoped<IFrequencyDivisionService, FrequencyDivisionService>();  

// Нові candidate-сервіси 
builder.Services.AddScoped<IContextSuggestionService, ContextSuggestionService>();
builder.Services.AddScoped<IKnownParticipantSuggestionService, KnownParticipantSuggestionService>();
builder.Services.AddScoped<IParticipantCandidateAnalysisService, ParticipantCandidateAnalysisService>();
builder.Services.AddScoped<IParticipantCandidateGroupQueryService, ParticipantCandidateGroupQueryService>();

// Нові reports-сервіси 
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

// після Build(), до Run()
await app.AddMigrationDb();
app.Run();