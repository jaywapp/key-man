using System.Runtime.ExceptionServices;
using System.Windows;
using KeyMan.Services;

namespace KeyMan.Mcp;

public interface IKeyManClipboard
{
    string ReadText();
    void WriteText(string value);
}

public sealed class WindowsClipboard : IKeyManClipboard, IDisposable
{
    private readonly ClipboardAutoClear _autoClear = new(
        ClipboardOwnership.WriteText,
        ClipboardOwnership.ClearIfOwned,
        _ => Console.Error.WriteLine("KeyMan could not clear the clipboard on shutdown. Clear the copied value manually."));

    public string ReadText() => RunOnStaThread(Clipboard.GetText);

    public void WriteText(string value)
    {
        var pendingClear = _autoClear.CopyAsync(value, TimeSpan.FromSeconds(30));
        _ = ObserveClearAsync(pendingClear);
    }

    public void Dispose() => _autoClear.Dispose();

    private static async Task ObserveClearAsync(Task<bool> pendingClear)
    {
        try
        {
            await pendingClear.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            Console.Error.WriteLine("KeyMan could not clear the clipboard. Clear the copied value manually.");
        }
    }

    private static T RunOnStaThread<T>(Func<T> operation)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = operation();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return result!;
    }
}
