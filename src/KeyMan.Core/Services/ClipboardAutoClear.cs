namespace KeyMan.Services;

public sealed class ClipboardAutoClear(
    Func<string, uint> writeText,
    Func<uint, bool> clearIfOwned,
    Action<Exception>? reportCleanupError = null) : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private uint? _ownership;
    private bool _disposed;

    public Task<bool> CopyAsync(string value, TimeSpan clearAfter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(clearAfter, TimeSpan.Zero);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var ownership = writeText(value);
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            _ownership = ownership;
            return ClearAfterAsync(ownership, clearAfter, _cancellation.Token);
        }
    }

    private async Task<bool> ClearAfterAsync(uint ownership, TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || _ownership != ownership)
            {
                return false;
            }

            var cleared = clearIfOwned(ownership);
            _ownership = null;
            return cleared;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            if (_ownership is { } ownership)
            {
                try
                {
                    clearIfOwned(ownership);
                }
                catch (Exception exception)
                {
                    reportCleanupError?.Invoke(exception);
                }
                finally
                {
                    _ownership = null;
                }
            }
        }
    }
}
