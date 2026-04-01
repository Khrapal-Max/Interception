using System.Windows.Input;
using Interception.Desktop.Wpf.Infrastructure;

namespace Interception.Desktop.Wpf.ViewModels;

/// <summary>
/// Головна модель представлення shell-розмітки з маршрутизацією модулів.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private object _currentViewModel;
    private string _statusMessage;

    public ShellViewModel()
    {
        NavigateHomeCommand = new RelayCommand(() => NavigateTo("Home"));
        NavigateObservationsCommand = new RelayCommand(() => NavigateTo("Observations"));
        NavigateAnalyticsCommand = new RelayCommand(() => NavigateTo("Analytics"));
        NavigateReportsCommand = new RelayCommand(() => NavigateTo("Reports"));
        NavigateRegistriesCommand = new RelayCommand(() => NavigateTo("Registries"));
        NavigateSettingsCommand = new RelayCommand(() => NavigateTo("Settings"));

        _currentViewModel = new HomeViewModel();
        _statusMessage = "Готово. Активна сторінка: Home";
    }

    public ICommand NavigateHomeCommand { get; }
    public ICommand NavigateObservationsCommand { get; }
    public ICommand NavigateAnalyticsCommand { get; }
    public ICommand NavigateReportsCommand { get; }
    public ICommand NavigateRegistriesCommand { get; }
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
            "Home" => new HomeViewModel(),
            "Observations" => new ObservationsViewModel(),
            "Analytics" => new AnalyticsViewModel(),
            "Reports" => new ReportsViewModel(),
            "Registries" => new RegistriesViewModel(),
            "Settings" => new SettingsViewModel(),
            _ => new HomeViewModel()
        };

        StatusMessage = $"Готово. Активна сторінка: {route}";
    }
}
