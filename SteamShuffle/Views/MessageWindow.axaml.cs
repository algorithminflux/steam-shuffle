using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SteamShuffle.Views;

// Avalonia has no built-in MessageBox; this replaces every
// MessageBox.Show(...) call from the WPF version.
public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message) : this()
    {
        Title = title;
        MessageText.Text = message;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close();

    public static Task ShowAsync(Window owner, string title, string message) =>
        new MessageWindow(title, message).ShowDialog(owner);
}
