using System.Runtime.ExceptionServices;
using System.Windows;

namespace KeyMan.Mcp;

public interface IKeyManClipboard
{
    string ReadText();
    void WriteText(string value);
}

public sealed class WindowsClipboard : IKeyManClipboard
{
    public string ReadText() => RunOnStaThread(Clipboard.GetText);

    public void WriteText(string value) => RunOnStaThread(() =>
    {
        Clipboard.SetText(value);
        return true;
    });

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
