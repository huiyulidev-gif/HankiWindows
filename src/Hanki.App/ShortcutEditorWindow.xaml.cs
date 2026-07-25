using System.Windows;
using Hanki.Core.Models;
using Hanki.Core.Services;

namespace Hanki.App;

public partial class ShortcutEditorWindow : Window
{
    private readonly ShortcutItem _source;
    private readonly ShortcutValidator _validator = new();

    public ShortcutEditorWindow(ShortcutItem? shortcut)
    {
        InitializeComponent();
        _source = shortcut?.Clone() ?? new ShortcutItem();
        Title = shortcut is null ? "새 단축어" : "단축어 수정";
        TitleBox.Text = _source.Title ?? string.Empty;
        TriggerBox.Text = _source.TriggerText;
        ReplacementBox.Text = _source.ReplacementText;
        FavoriteBox.IsChecked = _source.IsFavorite;
        Loaded += (_, _) => TriggerBox.Focus();
    }

    public ShortcutItem? Result { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var candidate = _source.Clone();
        candidate.Title = TitleBox.Text;
        candidate.TriggerText = TriggerBox.Text;
        candidate.ReplacementText = ReplacementBox.Text;
        candidate.IsFavorite = FavoriteBox.IsChecked == true;
        try
        {
            Result = _validator.NormalizeAndValidate(candidate);
            DialogResult = true;
        }
        catch (Hanki.Core.Exceptions.ShortcutValidationException exception)
        {
            MessageBox.Show(exception.Message, "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
