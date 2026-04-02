using System.IO;
using System.Windows;
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
using Interception.Desktop.Wpf.ViewModels;
using Interception.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WpfApplication = System.Windows.Application;

namespace Interception.Desktop.Wpf;

public partial class App : WpfApplication
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = CreateHostBuilder().Build();
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Помилка запуску застосунку",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = BuildConnectionString(context.Configuration);

                services.AddDbContextFactory<SqliteDbContext>(builder =>
                {
                    builder.UseSqlite(connectionString);
                    builder.EnableDetailedErrors();
                    builder.EnableSensitiveDataLogging();
                });

                services.AddDatabaseStartupMigration();

                RegisterApplicationServices(services);
                RegisterDesktopServices(services);
            });
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<ToastService>();

        services.AddScoped<IInterceptionQueryService, InterceptionQueryService>();
        services.AddScoped<IInterceptionCommandService, InterceptionCommandService>();
        services.AddScoped<IInterceptionSuggestionService, InterceptionSuggestionService>();

        services.AddScoped<IInterceptionImportService, InterceptionImportService>();
        services.AddScoped<ExcelImportParser>();

        services.AddScoped<IPersonRegistryService, PersonRegistryService>();
        services.AddScoped<IInterceptionActionService, InterceptionActionService>();
        services.AddScoped<IFrequencyDivisionService, FrequencyDivisionService>();

        services.AddScoped<IContextSuggestionService, ContextSuggestionService>();
        services.AddScoped<IKnownParticipantSuggestionService, KnownParticipantSuggestionService>();
        services.AddScoped<IParticipantCandidateAnalysisService, ParticipantCandidateAnalysisService>();
        services.AddScoped<IParticipantCandidateGroupQueryService, ParticipantCandidateGroupQueryService>();
        services.AddScoped<IParticipantCandidateGroupCommandService, ParticipantCandidateGroupCommandService>();
        services.AddScoped<ILinkMapService, LinkMapService>();

        services.AddScoped<IDivisionReportService, DivisionReportService>();
        services.AddScoped<IDayPictureService, DayPictureService>();
    }

    private static void RegisterDesktopServices(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ShellViewModel>();

        services.AddTransient<HomeViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ObservationsViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<RegistriesViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(rawConnectionString))
            rawConnectionString = "Data Source=data/interception.desktop.db";

        var builder = new SqliteConnectionStringBuilder(rawConnectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource))
            builder.DataSource = "data/interception.desktop.db";

        if (!Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, builder.DataSource));
        }

        var directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        return builder.ToString();
    }
}
