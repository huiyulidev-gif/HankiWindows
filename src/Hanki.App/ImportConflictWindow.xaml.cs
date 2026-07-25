using System.Windows;
using System.Windows.Controls;
using Hanki.Core.Models;

namespace Hanki.App;

public partial class ImportConflictWindow : Window
{
    public ImportConflictWindow() => InitializeComponent();

    public ImportConflictStrategy Strategy { get; private set; } = ImportConflictStrategy.Skip;

    private void Choice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value } &&
            Enum.TryParse<ImportConflictStrategy>(value, out var strategy))
        {
            Strategy = strategy;
            DialogResult = true;
        }
    }
}
