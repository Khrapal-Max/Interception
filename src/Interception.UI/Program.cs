//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Catalogs.Abstractions;
using Interception.UI.Application.Catalogs.Services;
using Interception.UI.Application.Hypotheses.Abstractions;
using Interception.UI.Application.Hypotheses.Services;
using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Services;
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

builder.Services.AddScoped<IObservationActionCatalogService, ObservationActionCatalogService>();
builder.Services.AddScoped<IResolvedSubdivisionCatalogService, ResolvedSubdivisionCatalogService>();
builder.Services.AddScoped<ITagCatalogService, TagCatalogService>();

builder.Services.AddScoped<IActorHypothesisService, ActorHypothesisService>();
builder.Services.AddScoped<ISubdivisionHypothesisService, SubdivisionHypothesisService>();

builder.Services.AddScoped<IObservationImportService, ObservationImportService>();
builder.Services.AddScoped<IObservationLookupService, ObservationLookupService>();
builder.Services.AddScoped<IObservationRegistryService, ObservationRegistryService>();
builder.Services.AddScoped<IObservationWriteService, ObservationWriteService>();

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