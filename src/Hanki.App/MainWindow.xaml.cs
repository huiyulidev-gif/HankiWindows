using System.Windows;
using System.Windows.Input;
using Hanki.App.ViewModels;
using Hanki.Core.Models;
using Microsoft.Win32;

namespace Hanki.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel, AccountViewModel accountViewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Account = accountViewModel;
        DataContext = viewModel;
        AccountTabRoot.DataContext = accountViewModel;
        viewModel.EditRequested += OnEditRequested;
        viewModel.DeleteRequested += OnDeleteRequested;
    }

    public MainViewModel ViewModel { get; }
    public AccountViewModel Account { get; }

    public void OpenNewShortcut() => OnEditRequested(this, null);
    public void ShowSettings() => MainTabs.SelectedIndex = 1;

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !Account.IsLoggingIn)
            return;

        if (Account.CancelCommand.CanExecute(null))
            Account.CancelCommand.Execute(null);
        e.Handled = true;
    }

    private async void OnEditRequested(object? sender, ShortcutItem? shortcut)
    {
        var isNew = shortcut is null;
        var dialog = new ShortcutEditorWindow(shortcut) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
            return;
        try
        {
            await ViewModel.SaveShortcutAsync(dialog.Result, isNew);
        }
        catch
        {
            MessageBox.Show(ViewModel.StatusMessage, "한키", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnDeleteRequested(object? sender, ShortcutItem shortcut)
    {
        var display = string.IsNullOrWhiteSpace(shortcut.Title) ? shortcut.TriggerText : shortcut.Title;
        if (MessageBox.Show($"'{display}' 단축어를 삭제할까요?",
                "단축어 삭제", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            await ViewModel.DeleteShortcutAsync(shortcut);
        }
        catch
        {
            MessageBox.Show(ViewModel.StatusMessage, "한키", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.SaveSettingsAsync();
        }
        catch
        {
            MessageBox.Show(ViewModel.StatusMessage, "한키", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "한키 JSON 백업 내보내기",
            Filter = "JSON 파일 (*.json)|*.json",
            FileName = $"Hanki-backup-{DateTime.Now:yyyyMMdd}.json",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            await ViewModel.ExportAsync(dialog.FileName);
            MessageBox.Show("백업을 저장했습니다.", "한키", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch
        {
            MessageBox.Show(ViewModel.StatusMessage, "한키", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "한키 JSON 백업 가져오기",
            Filter = "JSON 파일 (*.json)|*.json",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;
        var conflictDialog = new ImportConflictWindow { Owner = this };
        if (conflictDialog.ShowDialog() != true)
            return;
        try
        {
            var result = await ViewModel.ImportAsync(dialog.FileName, conflictDialog.Strategy);
            if (result is not null)
            {
                MessageBox.Show(
                    $"가져오기 {result.Imported}개 · 덮어쓰기 {result.Updated}개 · " +
                    $"이름 바꾸기 {result.Renamed}개 · 건너뛰기 {result.Skipped}개",
                    "한키", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch
        {
            MessageBox.Show(ViewModel.StatusMessage, "한키", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e) => ViewModel.OpenDataFolder();
}
