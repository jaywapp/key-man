using KeyMan.Mcp;
using KeyMan.Services;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using System.Reflection;

var testDirectory = Path.Combine(Path.GetTempPath(), "KeyMan.McpTests", Guid.NewGuid().ToString("N"));
var metadataStore = new MetadataStore(Path.Combine(testDirectory, "vault.json"));
var credentialStore = new WindowsCredentialStore();
var clipboard = new TestClipboard();
var tools = new KeyManTools(metadataStore, credentialStore, clipboard);
var variableName = $"KEYMAN_MCP_TEST_{Guid.NewGuid():N}";
var secret = $"local-only-{Guid.NewGuid():N}";

try
{
    await VerifyProtocolAsync(Path.Combine(testDirectory, "protocol-vault.json"));

    Environment.SetEnvironmentVariable(variableName, secret);
    var registered = await tools.RegisterFromEnvironmentAsync(
        "MCP smoke test",
        variableName,
        "KeyMan",
        "Test");
    Assert(registered.Success, "Registration did not report success.");
    Assert(!registered.ToString().Contains(secret, StringComparison.Ordinal), "Registration result exposed the secret.");

    var entries = await tools.ListAsync("MCP smoke");
    Assert(entries.Count == 1, "List did not return the registered entry.");
    var storedEntries = await metadataStore.LoadAsync();
    Assert(storedEntries.Count == 1, "Metadata was not persisted.");
    Assert(credentialStore.Read(storedEntries[0].Id) == secret, "Credential Manager did not preserve the secret.");

    File.SetAttributes(Path.Combine(testDirectory, "vault.json"), FileAttributes.ReadOnly);
    clipboard.WriteText("replacement-fixture");
    try
    {
        await AssertThrowsAsync<UnauthorizedAccessException>(() => tools.RegisterFromClipboardAsync(
            "MCP smoke test", "KeyMan", "Test", force: true));
        Assert(credentialStore.Read(storedEntries[0].Id) == secret, "A rejected replacement did not restore the previous secret.");
        await AssertThrowsAsync<UnauthorizedAccessException>(() => tools.RemoveAsync("MCP smoke test", "KeyMan", "Test"));
        Assert(credentialStore.Read(storedEntries[0].Id) == secret, "A rejected removal did not restore the previous secret.");
        Assert((await tools.ListAsync()).Count == 1, "A rejected removal changed metadata.");
    }
    finally
    {
        File.SetAttributes(Path.Combine(testDirectory, "vault.json"), FileAttributes.Normal);
    }

    foreach (var methodName in new[] { nameof(KeyManTools.RegisterFromClipboardAsync), nameof(KeyManTools.RegisterFromEnvironmentAsync) })
    {
        var annotation = typeof(KeyManTools).GetMethod(methodName)!.GetCustomAttribute<McpServerToolAttribute>();
        Assert(annotation?.Destructive == true, "A force-capable registration tool must be marked destructive.");
    }

    var copied = await tools.CopyAsync("MCP smoke test", "KeyMan", "Test");
    Assert(copied.Success && clipboard.Text == secret, "Copy did not write the secret to the local clipboard adapter.");
    Assert(!copied.ToString().Contains(secret, StringComparison.Ordinal), "Copy result exposed the secret.");

    var removed = await tools.RemoveAsync("MCP smoke test", "KeyMan", "Test");
    Assert(removed.Success, "Removal did not report success.");
    Assert((await tools.ListAsync()).Count == 0, "Removal left metadata behind.");

    Console.WriteLine("PASS: MCP tools keep secret values out of tool results");
    return 0;
}
finally
{
    Environment.SetEnvironmentVariable(variableName, null);
    foreach (var entry in await metadataStore.LoadAsync())
    {
        credentialStore.Delete(entry.Id);
    }

    if (Directory.Exists(testDirectory))
    {
        Directory.Delete(testDirectory, true);
    }
}

static async Task VerifyProtocolAsync(string vaultPath)
{
    var publishedServer = Environment.GetEnvironmentVariable("KEYMAN_MCP_TEST_SERVER");
    var command = publishedServer ?? "dotnet";
    var arguments = publishedServer is null
        ? new[] { Path.Combine(AppContext.BaseDirectory, "key-man-mcp.dll") }
        : Array.Empty<string>();
    await using var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "KeyMan MCP smoke test",
        Command = command,
        Arguments = arguments,
        EnvironmentVariables = new Dictionary<string, string?>
        {
            ["KEYMAN_VAULT_PATH"] = vaultPath
        }
    }));

    var tools = await client.ListToolsAsync();
    var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
    var expected = new[]
    {
        "keyman_list",
        "keyman_copy",
        "keyman_register_from_clipboard",
        "keyman_register_from_environment",
        "keyman_remove"
    };
    Assert(expected.All(names.Contains), "MCP initialization did not expose all expected tools.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static async Task AssertThrowsAsync<T>(Func<Task> operation) where T : Exception
{
    try
    {
        await operation();
    }
    catch (T)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

internal sealed class TestClipboard : IKeyManClipboard
{
    public string Text { get; private set; } = string.Empty;

    public string ReadText() => Text;

    public void WriteText(string value) => Text = value;
}
