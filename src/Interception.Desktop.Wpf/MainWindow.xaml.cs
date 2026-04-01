using Interception.Desktop.Wpf.ViewModels;

namespace Interception.Desktop.Wpf;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellViewModel();
    }
}
