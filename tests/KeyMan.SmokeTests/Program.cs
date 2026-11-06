using System.ComponentModel;
using KeyMan.Models;
using KeyMan.Services;

var failures = new List<string>();
await VerifyMetadataStoreAsync(failures);
VerifyWindowsCredentialStore(failures);
await ImporterTests.RunAsync(failures);

if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"FAIL: {failure}");
    }

    return 1;
}

Console.WriteLine("PASS: metadata and Windows credential storage smoke tests");
return 0;

static async Task VerifyMetadataStoreAsync(ICollection<string> failures)
{
    var testDirectory = Path.Combine(Path.GetTempPath(), "KeyMan.SmokeTests", Guid.NewGuid().ToString("N"));
    var filePath = Path.Combine(testDirectory, "vault.json");
    try
    {
        var store = new MetadataStore(filePath);
        var expected = new CredentialEntry
        {
            Id = Guid.NewGuid(),
            Name = "Smoke test entry",
            Service = "Local test",
            Environment = "Development",
            Notes = "Non-sensitive test data",
            CreatedAt = DateTimeOffset.Now
        };

        await store.MutateAsync(transaction => transaction.Entries.Add(expected));
        var actual = await store.LoadAsync();
        if (actual.Count != 1 || actual[0].Id != expected.Id || actual[0].Name != expected.Name)
        {
            failures.Add("Metadata round trip did not preserve the entry.");
        }
    }
    catch (Exception exception)
    {
        failures.Add($"Metadata round trip raised {exception.GetType().Name}.");
    }
    finally
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, true);
        }
    }
}

static void VerifyWindowsCredentialStore(ICollection<string> failures)
{
    var store = new WindowsCredentialStore();
    var id = Guid.NewGuid();
    var testValue = $"keyman-smoke-test-{Guid.NewGuid():N}";
    try
    {
        store.Save(id, testValue);
        if (store.Read(id) != testValue)
        {
            failures.Add("Credential round trip returned a different value.");
        }

        store.Delete(id);
        try
        {
            store.Read(id);
            failures.Add("Deleted credential could still be read.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1168)
        {
        }
    }
    catch (Exception exception)
    {
        failures.Add($"Credential round trip raised {exception.GetType().Name}.");
    }
    finally
    {
        store.Delete(id);
    }
}
