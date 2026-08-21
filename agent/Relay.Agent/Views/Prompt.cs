using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Relay.Agent.Views;

/// <summary>A small modal text-input dialog, themed to match the app.</summary>
public static class Prompt
{
    public static string? Text(FrameworkElement owner, string title, string initial)
    {
        var tb = new TextBox { Text = initial, Margin = new Thickness(16, 16, 16, 8), MinWidth = 260 };
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Style = (Style)owner.FindResource("AccentButton") };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 0, 16, 12) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var panel = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(tb);

        var win = new Window
        {
            Title = title, SizeToContent = SizeToContent.Height, Width = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Window.GetWindow(owner),
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow,
            Background = (Brush)owner.FindResource("Bg"), Content = panel,
        };
        string? result = null;
        ok.Click += (_, _) => { result = tb.Text; win.DialogResult = true; };
        return win.ShowDialog() == true ? result : null;
    }
}
