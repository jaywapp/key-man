using System.Text.Json;
using System.Windows;
using KeyMan.Models;
using KeyMan.Services;

namespace KeyMan.Cli;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            return new CliApplication(new MetadataStore(), new WindowsCredentialStore()).Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"keyman: {exception.Message}");
            return 1;
        }
    }
}

internal sealed class CliApplication(MetadataStore metadataStore, WindowsCredentialStore credentialStore)
{
    private const int UsageError = 2;
    private const int NotFound = 3;
    private const int Ambiguous = 4;

    public int Run(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();
        return command switch
        {
            "register" => Register(commandArgs),
            "get" => Get(commandArgs),
            "copy" => Copy(commandArgs),
            "list" => List(commandArgs),
            "remove" or "delete" => Remove(commandArgs),
            "help" => ShowHelp(),
            _ => UnknownCommand(command)
        };
    }

    private int Register(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintRegisterHelp();
            return args.Length == 0 ? UsageError : 0;
        }

        var name = args[0].Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Fail("A non-empty name is required.", UsageError);
        }

        var valueIndex = 1;
        string? inlineSecret = null;
        if (args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal))
        {
            inlineSecret = args[1];
            valueIndex = 2;
        }

        if (!TryParseOptions(
                args,
                valueIndex,
                out var options,
                out var optionError,
                ["stdin", "force"],
                ["service", "environment", "expires", "notes"]))
        {
            return Fail(optionError, UsageError);
        }

        var service = (options.GetValueOrDefault("service") ?? "General").Trim();
        var environment = (options.GetValueOrDefault("environment") ?? "Development").Trim();
        var notes = (options.GetValueOrDefault("notes") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(service) || string.IsNullOrWhiteSpace(environment))
        {
            return Fail("Service and environment must not be empty.", UsageError);
        }

        DateOnly? expiresOn = null;
        if (options.TryGetValue("expires", out var expirationText) && expirationText is not null)
        {
            if (!DateOnly.TryParseExact(expirationText, "yyyy-MM-dd", out var expiration))
            {
                return Fail("--expires must use YYYY-MM-DD.", UsageError);
            }

            expiresOn = expiration;
        }

        var secret = ReadSecret(inlineSecret, options.ContainsKey("stdin"));
        if (string.IsNullOrEmpty(secret))
        {
            return Fail("A non-empty token is required.", UsageError);
        }

        var entries = metadataStore.LoadAsync().GetAwaiter().GetResult().ToList();
        var existing = entries.SingleOrDefault(entry =>
            entry.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase) &&
            entry.Service.Equals(service, StringComparison.CurrentCultureIgnoreCase) &&
            entry.Environment.Equals(environment, StringComparison.CurrentCultureIgnoreCase));

        if (existing is not null && !options.ContainsKey("force"))
        {
            return Fail("An entry with the same name, service, and environment already exists. Use --force to replace it.", UsageError);
        }

        if (existing is not null)
        {
            var previousSecret = credentialStore.Read(existing.Id);
            var previousNotes = existing.Notes;
            var previousExpiration = existing.ExpiresOn;
            try
            {
                credentialStore.Save(existing.Id, secret);
                existing.Notes = notes;
                existing.ExpiresOn = expiresOn;
                metadataStore.SaveAsync(entries).GetAwaiter().GetResult();
            }
            catch
            {
                credentialStore.Save(existing.Id, previousSecret);
                existing.Notes = previousNotes;
                existing.ExpiresOn = previousExpiration;
                throw;
            }

            Console.WriteLine($"Updated: {existing.Name} [{existing.Service}/{existing.Environment}]");
            return 0;
        }

        var entry = new CredentialEntry
        {
            Id = Guid.NewGuid(),
            Name = name,
            Service = service,
            Environment = environment,
            Notes = notes,
            ExpiresOn = expiresOn,
            CreatedAt = DateTimeOffset.Now
        };

        credentialStore.Save(entry.Id, secret);
        entries.Add(entry);
        try
        {
            metadataStore.SaveAsync(entries).GetAwaiter().GetResult();
        }
        catch
        {
            credentialStore.Delete(entry.Id);
            throw;
        }

        Console.WriteLine($"Registered: {entry.Name} [{entry.Service}/{entry.Environment}]");
        return 0;
    }

    private int Get(string[] args)
    {
        var result = ResolveEntry(args, "get");
        if (result.ExitCode != 0)
        {
            return result.ExitCode;
        }

        Console.Out.Write(credentialStore.Read(result.Entry!.Id));
        return 0;
    }

    private int Copy(string[] args)
    {
        var result = ResolveEntry(args, "copy");
        if (result.ExitCode != 0)
        {
            return result.ExitCode;
        }

        Clipboard.SetText(credentialStore.Read(result.Entry!.Id));
        Console.WriteLine($"Copied: {result.Entry.Name}");
        return 0;
    }

    private int List(string[] args)
    {
        var query = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
        if (!TryParseOptions(args, query is null ? 0 : 1, out var options, out var optionError, ["json"], []))
        {
            return Fail(optionError, UsageError);
        }

        var entries = metadataStore.LoadAsync().GetAwaiter().GetResult()
            .Where(entry => string.IsNullOrWhiteSpace(query) ||
                entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                entry.Service.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(entry => entry.Service)
            .ThenBy(entry => entry.Name)
            .ToList();

        if (options.ContainsKey("json"))
        {
            var output = entries.Select(entry => new
            {
                entry.Name,
                entry.Service,
                entry.Environment,
                ExpiresOn = entry.ExpiresOn?.ToString("yyyy-MM-dd")
            });
            Console.WriteLine(JsonSerializer.Serialize(output));
            return 0;
        }

        foreach (var entry in entries)
        {
            Console.WriteLine($"{entry.Name}\t{entry.Service}\t{entry.Environment}\t{entry.ExpiresOn?.ToString("yyyy-MM-dd") ?? "-"}");
        }

        return 0;
    }

    private int Remove(string[] args)
    {
        var result = ResolveEntry(args, "remove", "yes");
        if (result.ExitCode != 0)
        {
            return result.ExitCode;
        }

        if (!result.Options.ContainsKey("yes"))
        {
            Console.Error.Write($"Remove '{result.Entry!.Name}'? [y/N] ");
            var answer = Console.ReadLine();
            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Cancelled.");
                return 0;
            }
        }

        var entries = metadataStore.LoadAsync().GetAwaiter().GetResult().ToList();
        var entry = entries.Single(item => item.Id == result.Entry!.Id);
        entries.Remove(entry);
        metadataStore.SaveAsync(entries).GetAwaiter().GetResult();
        try
        {
            credentialStore.Delete(entry.Id);
        }
        catch
        {
            entries.Add(entry);
            metadataStore.SaveAsync(entries).GetAwaiter().GetResult();
            throw;
        }

        Console.WriteLine($"Removed: {entry.Name}");
        return 0;
    }

    private ResolveResult ResolveEntry(string[] args, string command, params string[] additionalFlags)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            Console.Error.WriteLine($"Usage: keyman {command} <name> [--service <service>] [--environment <environment>]");
            return new ResolveResult(null, UsageError, new Dictionary<string, string?>());
        }

        if (!TryParseOptions(
                args,
                1,
                out var options,
                out var optionError,
                additionalFlags,
                ["service", "environment"]))
        {
            return new ResolveResult(null, Fail(optionError, UsageError), options);
        }

        var name = args[0];
        var matches = metadataStore.LoadAsync().GetAwaiter().GetResult()
            .Where(entry => entry.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
            .Where(entry => !options.TryGetValue("service", out var service) ||
                entry.Service.Equals(service, StringComparison.CurrentCultureIgnoreCase))
            .Where(entry => !options.TryGetValue("environment", out var environment) ||
                entry.Environment.Equals(environment, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            return new ResolveResult(null, Fail($"Entry not found: {name}", NotFound), options);
        }

        if (matches.Count > 1)
        {
            Console.Error.WriteLine($"keyman: Multiple entries match '{name}'. Add --service or --environment:");
            foreach (var match in matches)
            {
                Console.Error.WriteLine($"  {match.Name} [{match.Service}/{match.Environment}]");
            }
            return new ResolveResult(null, Ambiguous, options);
        }

        return new ResolveResult(matches[0], 0, options);
    }

    private static string ReadSecret(string? inlineSecret, bool readFromStandardInput)
    {
        if (inlineSecret is not null)
        {
            return inlineSecret;
        }

        if (readFromStandardInput || Console.IsInputRedirected)
        {
            return Console.In.ReadToEnd().TrimEnd('\r', '\n');
        }

        Console.Error.Write("Token: ");
        var characters = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return new string(characters.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace && characters.Count > 0)
            {
                characters.RemoveAt(characters.Count - 1);
            }
            else if (!char.IsControl(key.KeyChar))
            {
                characters.Add(key.KeyChar);
            }
        }
    }

    private static bool TryParseOptions(
        string[] args,
        int startIndex,
        out Dictionary<string, string?> options,
        out string error,
        IEnumerable<string> allowedFlags,
        IEnumerable<string> allowedValuedOptions)
    {
        options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;
        var flags = new HashSet<string>(allowedFlags, StringComparer.OrdinalIgnoreCase);
        var valuedOptions = new HashSet<string>(allowedValuedOptions, StringComparer.OrdinalIgnoreCase);

        for (var index = startIndex; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected argument: {argument}";
                return false;
            }

            var optionName = argument[2..];
            if (flags.Contains(optionName))
            {
                options[optionName] = null;
                continue;
            }

            if (!valuedOptions.Contains(optionName))
            {
                error = $"Unknown option: {argument}";
                return false;
            }

            if (++index >= args.Length)
            {
                error = $"Missing value for {argument}.";
                return false;
            }

            options[optionName] = args[index];
        }

        return true;
    }

    private static bool IsHelp(string argument) => argument is "-h" or "--help";

    private static int Fail(string message, int exitCode)
    {
        Console.Error.WriteLine($"keyman: {message}");
        return exitCode;
    }

    private static int UnknownCommand(string command) => Fail($"Unknown command: {command}", UsageError);

    private static int ShowHelp()
    {
        PrintHelp();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("KeyMan CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  keyman register <name> [token] [options]");
        Console.WriteLine("  keyman get <name> [--service <service>] [--environment <environment>]");
        Console.WriteLine("  keyman copy <name> [--service <service>] [--environment <environment>]");
        Console.WriteLine("  keyman list [query] [--json]");
        Console.WriteLine("  keyman remove <name> [--service <service>] [--environment <environment>] [--yes]");
        Console.WriteLine();
        Console.WriteLine("Run 'keyman register --help' for registration options.");
    }

    private static void PrintRegisterHelp()
    {
        Console.WriteLine("Usage: keyman register <name> [token] [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --stdin                    Read the token from standard input");
        Console.WriteLine("  --service <service>        Default: General");
        Console.WriteLine("  --environment <name>       Default: Development");
        Console.WriteLine("  --expires <YYYY-MM-DD>     Optional expiration date");
        Console.WriteLine("  --notes <text>             Optional non-sensitive notes");
        Console.WriteLine("  --force                    Replace an existing matching entry");
    }

    private sealed record ResolveResult(
        CredentialEntry? Entry,
        int ExitCode,
        Dictionary<string, string?> Options);
}
