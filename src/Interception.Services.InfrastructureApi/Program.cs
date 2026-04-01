var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "infrastructure-api",
    status = "ok",
    timestampUtc = DateTime.UtcNow
}));

app.MapGet("/infrastructure/metadata", () => Results.Ok(new
{
    storageLayer = "Interception.Infrastructure",
    version = "v1"
}));

app.Run();
