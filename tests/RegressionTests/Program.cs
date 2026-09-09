using KeyMan.Services;
using KeyMan.Models;
using System.Text.Json;
var directory = Path.Combine(Path.GetTempPath(), "KeyManRegression", Guid.NewGuid().ToString("N"));
try {
    var path = Path.Combine(directory, "metadata.json");
    var store = new MetadataStore(path);
    Check.That((await store.LoadAsync()).Count == 0, "Missing metadata");
    var entry = new CredentialEntry { Id = Guid.NewGuid(), Name = "Fixture", Service = "Test", Environment = "Local", CreatedAt = DateTimeOffset.UtcNow };
    await store.SaveAsync(new[] { entry });
    Check.That((await store.LoadAsync()).Single().Id == entry.Id, "Metadata round trip");
    await Check.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!), "Null cannot overwrite vault");
    Check.That((await store.LoadAsync()).Single().Id == entry.Id, "Null save preserves data");
    using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) {
        await Check.ThrowsAsync<IOException>(() => store.LoadAsync(), "Locked file failure remains visible");
    }
    File.WriteAllText(path, "{");
    await Check.ThrowsAsync<JsonException>(() => store.LoadAsync(), "Corrupt metadata must not silently become empty");
    File.WriteAllText(path, "null");
    Check.That((await store.LoadAsync()).Count == 0, "JSON null");
    await store.SaveAsync(Array.Empty<CredentialEntry>());
    Check.That((await store.LoadAsync()).Count == 0, "Empty save");
} finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
Console.WriteLine($"PASS {Check.Count} metadata regression checks");
