using System.Diagnostics;
using System.Text;
using KeyMan.Services;

internal static class ImporterTests
{
    public static async Task RunAsync(ICollection<string> failures)
    {
        var repository = FindRepository();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var cli = Environment.GetEnvironmentVariable("KEYMAN_CLI_TEST_EXE") ??
            Path.Combine(repository, "src", "KeyMan.Cli", "bin", configuration, "net10.0-windows", "keyman.exe");
        if (!File.Exists(cli))
        {
            failures.Add("Build the CLI before running importer tests or set KEYMAN_CLI_TEST_EXE.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "KeyMan.ImporterTests", Guid.NewGuid().ToString("N"));
        var root = Path.Combine(directory, "demo service's folder");
        Directory.CreateDirectory(root);
        var vaultPath = Path.Combine(directory, "isolated metadata", "vault.json");
        var metadata = new MetadataStore(vaultPath);
        var credentials = new WindowsCredentialStore();
        var fixture = "non-secret-한글-fixture-" + Guid.NewGuid().ToString("N");
        var envPath = Path.Combine(root, ".env");
        var importer = Path.Combine(repository, "tools", "Import-Env.ps1");
        try
        {
            var empty = await ImportAsync(importer, cli, root, vaultPath);
            Require(empty.ExitCode == 0 && !File.Exists(vaultPath), "An empty import must not create or damage a vault.");

            File.WriteAllText(envPath, "FIXTURE_ONE='" + fixture + "'\n", new UTF8Encoding(false));
            var one = await ImportAsync(importer, cli, root, vaultPath);
            var entries = await metadata.LoadAsync();
            Require(one.ExitCode == 0 && entries.Count == 1, "A one-entry import must create a readable array.");
            Require(credentials.Read(entries.Single().Id) == fixture, "Import stdin must preserve Unicode fixture text.");
            Require(!one.Output.Contains(fixture, StringComparison.Ordinal), "Importer output must not contain the input value.");

            File.WriteAllText(envPath,
                "FIXTURE_ONE='changed-fixture'\nFIXTURE_TWO=non-secret-two\nFIXTURE_THREE=non-secret-three\nEMPTY=\"\"\n",
                new UTF8Encoding(false));
            var many = await ImportAsync(importer, cli, root, vaultPath);
            entries = await metadata.LoadAsync();
            Require(many.ExitCode == 0 && entries.Count == 3, "A many-entry import must preserve existing entries and add new ones.");
            Require(credentials.Read(entries.Single(entry => entry.Name == "FIXTURE_ONE").Id) == fixture,
                "Duplicate import must not replace a previous credential.");

            await Task.WhenAll(Enumerable.Range(0, 6).Select(async index =>
            {
                var result = await RunProcessAsync(cli,
                    new[] { "register", "CONCURRENT_" + index, "--stdin", "--service", "ImporterTests" },
                    vaultPath, "non-secret-concurrent-fixture");
                Require(result.ExitCode == 0, "Concurrent CLI registration must succeed.");
            }));
            Require((await metadata.LoadAsync()).Count == 9, "Independent CLI processes must preserve every addition.");

            var corruptPath = Path.Combine(directory, "corrupt.json");
            File.WriteAllText(corruptPath, "{");
            var corrupt = await ImportAsync(importer, cli, root, corruptPath);
            Require(corrupt.ExitCode != 0 && File.ReadAllText(corruptPath) == "{", "Import must fail without replacing corrupt metadata.");

            Console.WriteLine("PASS: importer 0/1/many entries, Unicode stdin, duplicate preservation, corruption and concurrent CLI processes");
        }
        catch (Exception exception)
        {
            failures.Add($"Importer regression failed: {exception.Message}");
        }
        finally
        {
            foreach (var entry in await metadata.LoadAsync()) credentials.Delete(entry.Id);
            Directory.Delete(directory, true);
        }
    }

    private static Task<ProcessResult> ImportAsync(string importer, string cli, string root, string vaultPath) =>
        RunProcessAsync("powershell.exe", new[]
        {
            "-NoProfile", "-NonInteractive", "-File", importer,
            "-RootPath", root, "-KeyManPath", cli, "-VaultPath", vaultPath
        }, vaultPath, null);

    private static async Task<ProcessResult> RunProcessAsync(string executable, IEnumerable<string> arguments, string vaultPath, string? input)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false)
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        startInfo.Environment["KEYMAN_VAULT_PATH"] = vaultPath;
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the fixture process.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            if (input is not null) await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            return new ProcessResult(process.ExitCode, (await output) + (await error));
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "Import-Env.ps1"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Run importer tests from a KeyMan build in its repository.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
