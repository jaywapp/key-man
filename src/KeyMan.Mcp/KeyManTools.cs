using System.ComponentModel;
using KeyMan.Models;
using KeyMan.Services;
using ModelContextProtocol.Server;

namespace KeyMan.Mcp;

[McpServerToolType]
public sealed class KeyManTools(
    MetadataStore metadataStore,
    WindowsCredentialStore credentialStore,
    IKeyManClipboard clipboard)
{
    [McpServerTool(Name = "keyman_list", Title = "List KeyMan entries", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists KeyMan credential metadata. Secret values are never returned.")]
    public async Task<IReadOnlyList<CredentialSummary>> ListAsync(
        [Description("Optional text matched against credential name or service.")] string? query = null)
    {
        var entries = await metadataStore.LoadAsync();
        return entries
            .Where(entry => string.IsNullOrWhiteSpace(query) ||
                entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                entry.Service.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(entry => entry.Service)
            .ThenBy(entry => entry.Name)
            .Select(CredentialSummary.FromEntry)
            .ToList();
    }

    [McpServerTool(Name = "keyman_copy", Title = "Copy a KeyMan secret", ReadOnly = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Copies a credential to the local Windows clipboard without returning its secret value to the model.")]
    public async Task<OperationResult> CopyAsync(
        [Description("Credential name.")] string name,
        [Description("Optional service used to disambiguate matching names.")] string? service = null,
        [Description("Optional environment used to disambiguate matching names.")] string? environment = null)
    {
        var entry = await ResolveAsync(name, service, environment);
        clipboard.WriteText(credentialStore.Read(entry.Id));
        return Success($"Copied '{entry.Name}' to the local clipboard.", entry);
    }

    [McpServerTool(Name = "keyman_register_from_clipboard", Title = "Register from clipboard", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Stores the current local clipboard text in KeyMan. The secret is never sent through MCP.")]
    public Task<OperationResult> RegisterFromClipboardAsync(
        [Description("Credential name.")] string name,
        [Description("Service name. Defaults to General.")] string service = "General",
        [Description("Environment name. Defaults to Development.")] string environment = "Development",
        [Description("Optional non-sensitive notes.")] string notes = "",
        [Description("Optional expiration date in YYYY-MM-DD format.")] string? expiresOn = null,
        [Description("Replace a matching entry when true.")] bool force = false) =>
        RegisterAsync(name, clipboard.ReadText(), service, environment, notes, expiresOn, force);

    [McpServerTool(Name = "keyman_register_from_environment", Title = "Register from environment", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Stores a secret from a named local environment variable. The value is never sent through MCP.")]
    public Task<OperationResult> RegisterFromEnvironmentAsync(
        [Description("Credential name.")] string name,
        [Description("Name of the local environment variable containing the secret.")] string variable,
        [Description("Service name. Defaults to General.")] string service = "General",
        [Description("Environment name. Defaults to Development.")] string environment = "Development",
        [Description("Optional non-sensitive notes.")] string notes = "",
        [Description("Optional expiration date in YYYY-MM-DD format.")] string? expiresOn = null,
        [Description("Replace a matching entry when true.")] bool force = false)
    {
        variable = RequireText(variable, nameof(variable));
        var secret = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException($"Environment variable '{variable}' is missing or empty.", nameof(variable));
        }

        return RegisterAsync(name, secret, service, environment, notes, expiresOn, force);
    }

    [McpServerTool(Name = "keyman_remove", Title = "Remove a KeyMan entry", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Permanently removes a KeyMan credential and its local metadata.")]
    public async Task<OperationResult> RemoveAsync(
        [Description("Credential name.")] string name,
        [Description("Optional service used to disambiguate matching names.")] string? service = null,
        [Description("Optional environment used to disambiguate matching names.")] string? environment = null)
    {
        CredentialEntry? removed = null;
        await metadataStore.MutateAsync(transaction =>
        {
            var entry = Resolve(transaction.Entries, name, service, environment);
            string? previousSecret = null;
            try
            {
                previousSecret = credentialStore.Read(entry.Id);
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 1168)
            {
            }

            if (previousSecret is not null)
            {
                transaction.OnRollback(() => credentialStore.Save(entry.Id, previousSecret));
            }

            credentialStore.Delete(entry.Id);
            transaction.Entries.Remove(entry);
            removed = entry;
        });

        return Success($"Removed '{removed!.Name}'.", removed);
    }

    private async Task<OperationResult> RegisterAsync(
        string name,
        string secret,
        string service,
        string environment,
        string notes,
        string? expiresOn,
        bool force)
    {
        name = RequireText(name, nameof(name));
        service = RequireText(service, nameof(service));
        environment = RequireText(environment, nameof(environment));
        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException("The local secret source is empty.");
        }

        DateOnly? expiration = null;
        if (!string.IsNullOrWhiteSpace(expiresOn))
        {
            if (!DateOnly.TryParseExact(expiresOn, "yyyy-MM-dd", out var parsedExpiration))
            {
                throw new ArgumentException("Expiration must use YYYY-MM-DD.", nameof(expiresOn));
            }

            expiration = parsedExpiration;
        }

        CredentialEntry? registered = null;
        var updated = false;
        await metadataStore.MutateAsync(transaction =>
        {
            var existing = transaction.Entries.SingleOrDefault(entry =>
                entry.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase) &&
                entry.Service.Equals(service, StringComparison.CurrentCultureIgnoreCase) &&
                entry.Environment.Equals(environment, StringComparison.CurrentCultureIgnoreCase));
            if (existing is not null && !force)
            {
                throw new InvalidOperationException("A matching entry already exists. Set force to true to replace it.");
            }

            if (existing is not null)
            {
                var previousSecret = credentialStore.Read(existing.Id);
                transaction.OnRollback(() => credentialStore.Save(existing.Id, previousSecret));
                credentialStore.Save(existing.Id, secret);
                existing.Notes = notes.Trim();
                existing.ExpiresOn = expiration;
                registered = existing;
                updated = true;
                return;
            }

            var created = new CredentialEntry
            {
                Id = Guid.NewGuid(),
                Name = name,
                Service = service,
                Environment = environment,
                Notes = notes.Trim(),
                ExpiresOn = expiration,
                CreatedAt = DateTimeOffset.Now
            };
            transaction.OnRollback(() => credentialStore.Delete(created.Id));
            credentialStore.Save(created.Id, secret);
            transaction.Entries.Add(created);
            registered = created;
        });

        return Success($"{(updated ? "Updated" : "Registered")} '{registered!.Name}'.", registered);
    }

    private async Task<CredentialEntry> ResolveAsync(string name, string? service, string? environment) =>
        Resolve(await metadataStore.LoadAsync(), name, service, environment);

    private static CredentialEntry Resolve(IEnumerable<CredentialEntry> entries, string name, string? service, string? environment)
    {
        name = RequireText(name, nameof(name));
        var matches = entries
            .Where(entry => entry.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
            .Where(entry => string.IsNullOrWhiteSpace(service) || entry.Service.Equals(service, StringComparison.CurrentCultureIgnoreCase))
            .Where(entry => string.IsNullOrWhiteSpace(environment) || entry.Environment.Equals(environment, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => throw new KeyNotFoundException($"Entry not found: {name}"),
            > 1 => throw new InvalidOperationException($"Multiple entries match '{name}'. Specify service and environment."),
            _ => matches[0]
        };
    }

    private static string RequireText(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? throw new ArgumentException("A non-empty value is required.", parameterName)
            : trimmed;
    }

    private static OperationResult Success(string message, CredentialEntry entry) =>
        new(true, message, CredentialSummary.FromEntry(entry));
}

public sealed record CredentialSummary(string Name, string Service, string Environment, string? ExpiresOn)
{
    public static CredentialSummary FromEntry(CredentialEntry entry) =>
        new(entry.Name, entry.Service, entry.Environment, entry.ExpiresOn?.ToString("yyyy-MM-dd"));
}

public sealed record OperationResult(bool Success, string Message, CredentialSummary Entry);
