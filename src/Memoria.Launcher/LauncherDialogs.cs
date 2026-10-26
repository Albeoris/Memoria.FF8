using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Memoria.Launcher;

internal static class LauncherDialogs
{
    public static Task<bool> ConfirmAsync(Window owner, string title, string message) => Create(title, message, true).ShowDialog<bool>(owner);

    public static async Task ShowAsync(Window owner, string title, string message)
    {
        await Create(title, message, false).ShowDialog<bool>(owner);
    }

    public static Window Create(string title, string message, bool confirmation)
    {
        Window dialog = new() { Title = title, Width = 540, SizeToContent = SizeToContent.Height, MaxHeight = 720, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = true };
        StackPanel content = new() { Margin = new Thickness(24), Spacing = 20 };
        content.Children.Add(new ScrollViewer { MaxHeight = 540, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap } });
        StackPanel buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 12 };
        Button accept = new() { Content = confirmation ? "Continue" : "OK", MinWidth = 90, IsDefault = !confirmation };
        accept.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(accept);
        if (confirmation)
        {
            Button cancel = new() { Content = "Cancel", MinWidth = 90, IsCancel = true, IsDefault = true };
            cancel.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(cancel);
        }
        content.Children.Add(buttons);
        dialog.Content = content;
        return dialog;
    }
}
