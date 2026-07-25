using System.Windows;

namespace Hanki.App;

public partial class FirstRunWindow : Window
{
    public FirstRunWindow() => InitializeComponent();
    private void StartButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
