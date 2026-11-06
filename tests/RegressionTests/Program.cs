using KeyMan.Services;
using KeyMan.Models;
using System.Text.Json;

if (args.Length == 3 && args[0] == "--transaction-worker")
{
    var workerStore = new MetadataStore(args[1]);
    for (var index = 0; index < 8; index++)
    {
        var entry = TransactionTests.CreateEntry(args[2] + "-" + index);
        await workerStore.MutateAsync(transaction => transaction.Entries.Add(entry));
    }

    return;
}

var directory = Path.Combine(Path.GetTempPath(), "KeyManRegression", Guid.NewGuid().ToString("N"));
try
{
    var path = Path.Combine(directory, "metadata.json");
    var store = new MetadataStore(path);
    Check.That((await store.LoadAsync()).Count == 0, "Missing metadata");
    var entry = TransactionTests.CreateEntry("Fixture");
    await store.MutateAsync(transaction => transaction.Entries.Add(entry));
    Check.That((await store.LoadAsync()).Single().Id == entry.Id, "Metadata round trip");
    await Check.ThrowsAsync<ArgumentNullException>(() => store.MutateAsync(null!), "Null mutation cannot overwrite vault");
    Check.That((await store.LoadAsync()).Single().Id == entry.Id, "Null mutation preserves data");
    using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        await Check.ThrowsAsync<IOException>(() => store.LoadAsync(), "Locked file failure remains visible");
    }

    foreach (var invalidJson in new[] { "{", "", "null", "[null]", "{}" })
    {
        File.WriteAllText(path, invalidJson);
        await Check.ThrowsAsync<JsonException>(() => store.LoadAsync(), "Corrupt metadata is rejected");
        var invoked = false;
        await Check.ThrowsAsync<JsonException>(() => store.MutateAsync(_ => invoked = true), "Corruption blocks mutations");
        Check.That(!invoked && File.ReadAllText(path) == invalidJson, "Rejected mutation never touches corrupt data or credentials");
    }

    File.WriteAllText(path, "[]");
    await store.MutateAsync(transaction => transaction.Entries.Add(entry));
    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
    var rolledBack = false;
    try
    {
        await Check.ThrowsAsync<UnauthorizedAccessException>(() => store.MutateAsync(transaction =>
        {
            transaction.OnRollback(() => rolledBack = true);
            transaction.Entries.Clear();
        }), "Read-only metadata rejects writes");
        Check.That(rolledBack, "Failed metadata write rolls back credential changes");
        Check.That((await store.LoadAsync()).Single().Id == entry.Id, "Rejected write preserves metadata");
        Check.That(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "Rejected write cleans its temporary file");
    }
    finally
    {
        File.SetAttributes(path, FileAttributes.Normal);
    }

    await TransactionTests.RunAsync(directory);
}
finally
{
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}

Console.WriteLine($"PASS {Check.Count} metadata regression checks");
