using KeyMan.Services;

var checks = 0;
var clipboard = new FakeClipboard();
using (var lifetime = new ClipboardAutoClear(clipboard.Write, clipboard.ClearIfOwned))
{
    Assert(await lifetime.CopyAsync("fixture", TimeSpan.Zero), "Owned value clears at expiry.");
    Assert(clipboard.Text is null, "Expiry removed the owned value.");

    var pending = lifetime.CopyAsync("fixture", TimeSpan.FromMilliseconds(60));
    clipboard.Write("unrelated");
    Assert(!await pending, "An intervening copy is preserved.");
    Assert(clipboard.Text == "unrelated", "Unrelated clipboard content remains.");

    pending = lifetime.CopyAsync("same", TimeSpan.FromMilliseconds(60));
    clipboard.Write("same");
    Assert(!await pending, "An identical value copied by someone else is preserved.");

    var oldCopy = lifetime.CopyAsync("old", TimeSpan.FromMinutes(1));
    var latestCopy = lifetime.CopyAsync("latest", TimeSpan.FromMilliseconds(60));
    await AssertCanceled(oldCopy);
    Assert(await latestCopy, "The latest copy owns the expiry.");
}

clipboard = new FakeClipboard();
var owned = new ClipboardAutoClear(clipboard.Write, clipboard.ClearIfOwned);
var closingCopy = owned.CopyAsync("close-fixture", TimeSpan.FromMinutes(1));
owned.Dispose();
Assert(clipboard.Text is null, "Normal disposal clears the owned value immediately.");
await AssertCanceled(closingCopy);
owned.Dispose();
try
{
    _ = owned.CopyAsync("fixture", TimeSpan.Zero);
    throw new InvalidOperationException("Disposed lifetime accepted a copy.");
}
catch (ObjectDisposedException)
{
    checks++;
}

clipboard = new FakeClipboard();
owned = new ClipboardAutoClear(clipboard.Write, clipboard.ClearIfOwned);
closingCopy = owned.CopyAsync("fixture", TimeSpan.FromMinutes(1));
clipboard.Write("new-owner");
owned.Dispose();
Assert(clipboard.Text == "new-owner", "Disposal preserves another owner's value.");
await AssertCanceled(closingCopy);

var cleanupReported = false;
owned = new ClipboardAutoClear(_ => 1, _ => throw new IOException("Fixture failure."), _ => cleanupReported = true);
closingCopy = owned.CopyAsync("fixture", TimeSpan.FromMinutes(1));
owned.Dispose();
Assert(cleanupReported, "Shutdown cleanup failures are observable without crashing shutdown.");
await AssertCanceled(closingCopy);

Console.WriteLine($"PASS: {checks} clipboard lifetime checks (in-memory fixtures only)");
NativeClipboardTests.Run();

void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
    checks++;
}

async Task AssertCanceled(Task task)
{
    try
    {
        await task;
        throw new InvalidOperationException("Superseded timer was not canceled.");
    }
    catch (OperationCanceledException)
    {
        checks++;
    }
}

internal sealed class FakeClipboard
{
    private uint _sequence;
    public string? Text { get; private set; }

    public uint Write(string value)
    {
        Text = value;
        return ++_sequence;
    }

    public bool ClearIfOwned(uint sequence)
    {
        if (sequence != _sequence)
        {
            return false;
        }
        Text = null;
        _sequence++;
        return true;
    }
}
