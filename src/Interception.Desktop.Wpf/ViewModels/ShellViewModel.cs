using System.Windows.Input;
using Interception.Desktop.Wpf.Infrastructure;

namespace Interception.Desktop.Wpf.ViewModels;

/// <summary>
/// Головна модель представлення shell-розмітки з маршрутизацією сторінок.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private object _currentViewModel;
    private string _statusMessage;

    public ShellViewModel()
    {
        NavigateDashboardCommand = new RelayCommand(() => NavigateTo("Dashboard"));
        NavigateProjectsCommand = new RelayCommand(() => NavigateTo("Projects"));
        NavigateReportsCommand = new RelayCommand(() => NavigateTo("Reports"));
        NavigateSettingsCommand = new RelayCommand(() => NavigateTo("Settings"));

        _currentViewModel = new DashboardViewModel();
        _statusMessage = "Готово. Активна сторінка: Dashboard";
    }

    public ICommand NavigateDashboardCommand { get; }
    public ICommand NavigateProjectsCommand { get; }
    public ICommand NavigateReportsCommand { get; }
    public ICommand NavigateSettingsCommand { get; }

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private void NavigateTo(string route)
    {
        CurrentViewModel = route switch
        {
            "Dashboard" => new DashboardViewModel(),
            "Projects" => new ProjectsViewModel(),
            "Reports" => new ReportsViewModel(),
            "Settings" => new SettingsViewModel(),
            _ => new DashboardViewModel()
        };

        StatusMessage = $"Готово. Активна сторінка: {route}";
    }
}
