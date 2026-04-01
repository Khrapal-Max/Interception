using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Interception.Desktop.Wpf.Infrastructure;

/// <summary>
/// Базова модель представлення з підтримкою сповіщень про зміну властивостей.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
