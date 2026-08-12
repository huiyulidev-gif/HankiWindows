using System.ComponentModel;
using System.Diagnostics;
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

    private void RefreshDiagnosticsButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RefreshCompatibilityDiagnostics();

    private async void HookSelfTestButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunHookSelfTestAsync();
    }

    private async void RestartHookButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RestartHookAsync();
    }

    private void BeginInternalTestButton_Click(object sender, RoutedEventArgs e)
    {
        CompatibilityTestInput.Clear();
        ViewModel.BeginInternalExpansionTest();
        CompatibilityTestInput.Focus();
    }

    private async void ExportDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "한키 호환성 진단 ZIP 저장",
            Filter = "ZIP 파일 (*.zip)|*.zip",
            FileName = $"Hanki-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            await ViewModel.ExportCompatibilityDiagnosticsAsync(dialog.FileName);
            MessageBox.Show(
                "키 입력·단축어·변환문·클립보드·창 제목을 제외한 진단 ZIP을 저장했습니다.",
                "한키",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch
        {
            MessageBox.Show(
                "진단 ZIP을 저장하지 못했습니다. 쓰기 권한과 저장 위치를 확인해 주세요.",
                "한키",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void RestartElevatedButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "현재 설정을 저장한 뒤 이번 세션만 관리자 권한으로 한키를 다시 시작합니다.\n\n" +
            "관리자 실행은 게임 보안 모듈이나 PC방 보안 정책의 제한 해제를 보장하지 않습니다. 계속할까요?",
            "한키 관리자 재시작",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            await ViewModel.SaveSettingsAsync();
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("현재 실행 파일 경로를 확인할 수 없습니다.");
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true,
                Verb = "runas"
            });
            System.Windows.Application.Current.Shutdown();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            MessageBox.Show(
                "관리자 권한 요청을 취소했습니다. 한키는 현재 권한으로 계속 실행됩니다.",
                "한키",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch
        {
            MessageBox.Show(
                "관리자 권한으로 다시 시작하지 못했습니다. 한키는 현재 권한으로 계속 실행됩니다.",
                "한키",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
