var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "application-api",
    status = "ok",
    timestampUtc = DateTime.UtcNow
}));

app.MapGet("/application/metadata", () => Results.Ok(new
{
    scenarioLayer = "Interception.Application",
    version = "v1"
}));

app.Run();
