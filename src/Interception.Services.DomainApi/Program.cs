var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "domain-api",
    status = "ok",
    timestampUtc = DateTime.UtcNow
}));

app.MapGet("/domain/metadata", () => Results.Ok(new
{
    boundedContext = "Interception.Domain",
    version = "v1"
}));

app.Run();
