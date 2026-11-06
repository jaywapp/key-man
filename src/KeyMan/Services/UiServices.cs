using System.Windows;
using KeyMan.Models;
using KeyMan.ViewModels;

namespace KeyMan.Services;

public sealed record NewCredential(CredentialEntry Entry, string Secret);

public interface IDialogService
{
    NewCredential? ShowAddCredential();
    bool ConfirmDelete(string name);
    void ShowError(string message, Exception exception);
}

public interface IClipboardService : IDisposable
{
    Task<bool> CopyWithAutoClearAsync(string value, TimeSpan clearAfter);
}

public sealed class WpfDialogService(Window owner) : IDialogService
{
    public NewCredential? ShowAddCredential()
    {
        var viewModel = new AddCredentialViewModel();
        var dialog = new AddCredentialWindow(viewModel) { Owner = owner };
        return dialog.ShowDialog() == true
            ? new NewCredential(viewModel.CreateEntry(), viewModel.Secret)
            : null;
    }

    public bool ConfirmDelete(string name)
    {
        return MessageBox.Show(owner, $"'{name}'을(를) 영구 삭제할까요?", "키 삭제",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public void ShowError(string message, Exception exception)
    {
        MessageBox.Show(owner, $"{message}\n\n{exception.Message}", "KeyMan 오류",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

public sealed class WpfClipboardService : IClipboardService
{
    private readonly ClipboardAutoClear _autoClear = new(
        ClipboardOwnership.WriteText,
        ClipboardOwnership.ClearIfOwned,
        _ => MessageBox.Show("클립보드를 정리하지 못했습니다. 복사한 값을 직접 지워 주세요.",
            "KeyMan", MessageBoxButton.OK, MessageBoxImage.Warning));

    public Task<bool> CopyWithAutoClearAsync(string value, TimeSpan clearAfter) =>
        _autoClear.CopyAsync(value, clearAfter);

    public void Dispose() => _autoClear.Dispose();
}
