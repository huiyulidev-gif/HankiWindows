using System.Windows;

namespace Hanki.CompatibilityHarness;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        StandardInput.Clear();
        MultilineInput.Clear();
        PasswordInput.Clear();
        RapidInput.Clear();
        StandardInput.Focus();
    }
}
