using Interception.Application.Import.Abstractions;
using Interception.Application.Import.Services;
using Interception.Application.Interceptions.Abstractions;
using Interception.Application.Interceptions.Services;
using Interception.Application.Registry.Abstractions;
using Interception.Application.Registry.Services;
using Interception.Infrastructure.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WpfApplication = System.Windows.Application;

namespace Interception.Desktop.Wpf;

public partial class App : WpfApplication
{
    public static IServiceProvider Services { get; private set; } = new ServiceCollection().BuildServiceProvider();

    public App()
    {
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var connectionString =
            Environment.GetEnvironmentVariable("INTERCEPTION_DESKTOP_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("INTERCEPTION_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContextFactory<SqliteDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });

            services.AddScoped<IInterceptionQueryService, InterceptionQueryService>();
            services.AddScoped<IInterceptionCommandService, InterceptionCommandService>();
            services.AddScoped<IInterceptionSuggestionService, InterceptionSuggestionService>();
            services.AddScoped<IInterceptionImportService, InterceptionImportService>();
            services.AddScoped<IInterceptionActionService, InterceptionActionService>();
        }

        return services.BuildServiceProvider();
    }
}
