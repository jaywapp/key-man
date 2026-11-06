using System.Diagnostics;
using System.Reflection;
using KeyMan.Models;
using KeyMan.Services;

internal static class TransactionTests
{
    public static CredentialEntry CreateEntry(string name) => new()
    {
        Id = Guid.NewGuid(), Name = name, Service = "Test", Environment = "Local", CreatedAt = DateTimeOffset.UtcNow
    };

    public static async Task RunAsync(string directory)
    {
        var path = Path.Combine(directory, "transactions.json");
        var firstClient = new MetadataStore(path);
        var secondClient = new MetadataStore(path);
        var staleSnapshot = await firstClient.LoadAsync();
        var externalEntry = CreateEntry("External");
        await secondClient.MutateAsync(transaction => transaction.Entries.Add(externalEntry));
        var refreshed = await firstClient.MutateAsync(transaction => transaction.Entries.Add(CreateEntry("GUI")));
        Check.That(staleSnapshot.Count == 0 && refreshed.Count == 2 && refreshed.Any(entry => entry.Id == externalEntry.Id),
            "A client with a stale snapshot preserves external additions and receives the latest snapshot");
        await secondClient.MutateAsync(transaction => transaction.Entries.RemoveAll(entry => entry.Id == externalEntry.Id));
        refreshed = await firstClient.MutateAsync(transaction => transaction.Entries.Add(CreateEntry("After delete")));
        Check.That(refreshed.All(entry => entry.Id != externalEntry.Id), "A stale client cannot resurrect external deletions");

        var rollbackOrder = new List<int>();
        await Check.ThrowsAsync<InvalidOperationException>(() => firstClient.MutateAsync(transaction =>
        {
            transaction.OnRollback(() => rollbackOrder.Add(1));
            transaction.OnRollback(() => rollbackOrder.Add(2));
            transaction.Entries.Clear();
            throw new InvalidOperationException("Fixture failure");
        }), "Mutation failure is reported");
        Check.That(rollbackOrder.SequenceEqual(new[] { 2, 1 }), "Rollback runs in reverse order");
        Check.That((await firstClient.LoadAsync()).Count == 2, "Failed mutation keeps the previous metadata");

        var rollbackFinished = false;
        await Check.ThrowsAsync<AggregateException>(() => firstClient.MutateAsync(transaction =>
        {
            transaction.OnRollback(() => rollbackFinished = true);
            transaction.OnRollback(() => throw new IOException("Fixture rollback failure"));
            throw new InvalidOperationException("Fixture failure");
        }), "Rollback failures remain visible");
        Check.That(rollbackFinished, "One rollback failure does not prevent other rollbacks");

        var impatientClient = new MetadataStore(path, TimeSpan.FromMilliseconds(100));
        using (var heldLock = new FileStream(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var timer = Stopwatch.StartNew();
            await Check.ThrowsAsync<IOException>(() => impatientClient.MutateAsync(transaction => transaction.Entries.Clear()),
                "A busy vault fails after a bounded wait");
            Check.That(timer.Elapsed < TimeSpan.FromSeconds(2), "Lock timeout is bounded");
        }

        var workers = Enumerable.Range(0, 4).Select(index => StartWorker(path, "process-" + index)).ToArray();
        try
        {
            await Task.WhenAll(workers.Select(async worker =>
            {
                await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                Check.That(worker.ExitCode == 0, "Concurrent process exited successfully");
            }));
        }
        finally
        {
            foreach (var worker in workers)
            {
                if (!worker.HasExited) worker.Kill(entireProcessTree: true);
                worker.Dispose();
            }
        }

        var concurrentEntries = await firstClient.LoadAsync();
        Check.That(concurrentEntries.Count == 34 && concurrentEntries.Select(entry => entry.Id).Distinct().Count() == 34,
            "Independent processes preserve all 32 concurrent additions and previous entries");
        Check.That(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "Successful transactions leave no temporary files");
    }

    private static Process StartWorker(string path, string prefix)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The test executable path is missing.");
        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        }

        startInfo.ArgumentList.Add("--transaction-worker");
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add(prefix);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start a transaction worker.");
    }
}
