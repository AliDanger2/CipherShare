using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CipherShare.Common;

/// <summary>
/// Minimal base class that implements INotifyPropertyChanged.
/// Every model and view model that needs to update the UI automatically
/// (progress bars, status text, lists, etc.) derives from this.
/// </summary>
public class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets the backing field and raises PropertyChanged only if the value actually changed.
    /// Usage: Set(ref _name, value);
    /// </summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
