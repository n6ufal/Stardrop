using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Stardrop.Utilities.Internal;

namespace Stardrop.Views
{
    /// <summary>
    /// Hosts the transient messages reported through <see cref="NotificationCenter"/>. Reports nothing itself: the
    /// install paths push a notification and carry on, so nothing here can hold up an install.
    /// </summary>
    public partial class ToastHost : UserControl
    {
        public ToastHost()
        {
            AvaloniaXamlLoader.Load(this);

            if (Design.IsDesignMode)
            {
                return;
            }

            DataContext = NotificationCenter.Shared;
        }

        private void DismissButton_Click(object? sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is Notification notification)
            {
                NotificationCenter.Shared.Dismiss(notification);
            }
        }
    }
}
