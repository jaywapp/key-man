using System.Diagnostics;
using System.Text.Json;
using KeyMan.Models;

namespace KeyMan.Services;

public sealed class MetadataStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly TimeSpan _lockTimeout;

    public MetadataStore(string? filePath = null, TimeSpan? lockTimeout = null)
    {
        filePath ??= Environment.GetEnvironmentVariable("KEYMAN_VAULT_PATH");
        _filePath = Path.GetFullPath(filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMan", "vault.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        _lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(5);
        if (_lockTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lockTimeout));
        }
    }

    public async Task<IReadOnlyList<CredentialEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var fileLock = await AcquireLockAsync(cancellationToken);
        return await LoadCoreAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CredentialEntry>> MutateAsync(
        Action<MetadataTransaction> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await using var fileLock = await AcquireLockAsync(cancellationToken);
        var transaction = new MetadataTransaction(await LoadCoreAsync(cancellationToken));
        try
        {
            mutation(transaction);
            await SaveCoreAsync(transaction.Entries, cancellationToken);
            return transaction.Entries.ToList();
        }
        catch (Exception failure)
        {
            var rollbackFailures = transaction.Rollback();
            if (rollbackFailures.Count > 0)
            {
                throw new AggregateException("The vault update and its credential rollback failed.",
                    new[] { failure }.Concat(rollbackFailures));
            }

            throw;
        }
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(_filePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception)
            {
                if (elapsed.Elapsed >= _lockTimeout)
                {
                    throw new IOException("The KeyMan vault is busy. Try again after the other operation finishes.", exception);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }
        }
    }

    private async Task<List<CredentialEntry>> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var entries = await JsonSerializer.DeserializeAsync<List<CredentialEntry>>(stream, SerializerOptions, cancellationToken)
            ?? throw new JsonException("The KeyMan vault must contain a JSON array.");
        ValidateEntries(entries);
        return entries;
    }

    private async Task SaveCoreAsync(List<CredentialEntry> entries, CancellationToken cancellationToken)
    {
        ValidateEntries(entries);
        var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, entries, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidateEntries(List<CredentialEntry> entries)
    {
        var ids = new HashSet<Guid>();
        foreach (var entry in entries)
        {
            if (entry is null || entry.Id == Guid.Empty || !ids.Add(entry.Id) ||
                string.IsNullOrWhiteSpace(entry.Name) || entry.Service is null || entry.Environment is null || entry.Notes is null)
            {
                throw new JsonException("The KeyMan vault contains invalid or duplicate metadata.");
            }
        }
    }
}

public sealed class MetadataTransaction
{
    private readonly Stack<Action> _rollbackActions = new();

    internal MetadataTransaction(List<CredentialEntry> entries) => Entries = entries;

    public List<CredentialEntry> Entries { get; }

    public void OnRollback(Action rollback)
    {
        ArgumentNullException.ThrowIfNull(rollback);
        _rollbackActions.Push(rollback);
    }

    internal List<Exception> Rollback()
    {
        var failures = new List<Exception>();
        while (_rollbackActions.TryPop(out var rollback))
        {
            try
            {
                rollback();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }
}
