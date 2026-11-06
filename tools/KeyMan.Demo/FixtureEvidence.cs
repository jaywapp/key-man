using System.Diagnostics;
using System.IO;
using System.Text.Json;
using KeyMan.Models;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

internal sealed record FixtureEvidence(string Cli, string Mcp)
{
    public static async Task<FixtureEvidence> CollectAsync(string root, string directory, CredentialEntry[] entries)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var fixturePath = Path.Combine(directory, "metadata-fixture.json");
        await File.WriteAllTextAsync(fixturePath, JsonSerializer.Serialize(entries));
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(root, "src", "KeyMan.Cli", "bin", "Release", "net10.0-windows", "keyman.exe"),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("list");
        start.ArgumentList.Add("OpenAI");
        start.ArgumentList.Add("--json");
        start.Environment["KEYMAN_VAULT_PATH"] = fixturePath;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("CLI fixture process did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw new TimeoutException("The read-only CLI fixture exceeded its 30-second timeout.");
        }
        _ = await errors;
        var cli = (await output).Trim();
        if (process.ExitCode != 0) throw new InvalidOperationException("Read-only CLI fixture request failed.");
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "KeyMan read-only media fixture",
            Command = Path.Combine(root, "src", "KeyMan.Mcp", "bin", "Release", "net10.0-windows", "key-man-mcp.exe"),
            EnvironmentVariables = new Dictionary<string, string?> { ["KEYMAN_VAULT_PATH"] = fixturePath }
        }), cancellationToken: timeout.Token);
        var result = await client.CallToolAsync("keyman_list", new Dictionary<string, object?> { ["query"] = "OpenAI" },
            cancellationToken: timeout.Token);
        var mcp = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        if (!cli.Contains("OpenAI API Key", StringComparison.Ordinal) ||
            !mcp.Contains("OpenAI API Key", StringComparison.Ordinal) || result.IsError == true)
            throw new InvalidOperationException("CLI/MCP did not return the expected metadata-only fixture.");
        return new FixtureEvidence(cli, mcp);
    }
}
