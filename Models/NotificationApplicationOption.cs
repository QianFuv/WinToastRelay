using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;

namespace WinToastRelay.Models;

public partial class NotificationApplicationOption : ObservableObject
{
    public NotificationApplicationOption(string name, bool isEnabled, ImageSource? iconSource = null)
    {
        Name = name;
        IsEnabled = isEnabled;
        IconSource = iconSource;
    }

    public string Name { get; }
    public ImageSource? IconSource { get; }
    public Visibility FallbackVisibility => IconSource is null ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }
}
