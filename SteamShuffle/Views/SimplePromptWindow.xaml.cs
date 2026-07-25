using System.Windows;
using System.Windows.Input;

namespace SteamShuffle.Views;

public partial class SimplePromptWindow
{
    public string ResponseText => ResponseBox.Text;

    public SimplePromptWindow(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        Loaded += (_, _) => ResponseBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ResponseBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
            Close();
        }
    }
}