using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SteamShuffle.Views;

public partial class SimplePromptWindow : Window
{
    public string ResponseText => ResponseBox.Text ?? string.Empty;

    public SimplePromptWindow()
    {
        InitializeComponent();
        Opened += (_, _) => ResponseBox.Focus();
    }

    public SimplePromptWindow(string title, string prompt) : this()
    {
        Title = title;
        PromptLabel.Text = prompt;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void ResponseBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Close(true);
        }
    }

    // WPF exposed DialogResult as a settable property inspected after ShowDialog()
    // returned. Avalonia's ShowDialog<TResult> is itself generic -- the Task's
    // result is whatever gets passed to Close(...).
    public static async Task<string?> AskAsync(Window owner, string title, string prompt)
    {
        var dialog = new SimplePromptWindow(title, prompt);
        var confirmed = await dialog.ShowDialog<bool>(owner);
        return confirmed && !string.IsNullOrWhiteSpace(dialog.ResponseText) ? dialog.ResponseText.Trim() : null;
    }
}
