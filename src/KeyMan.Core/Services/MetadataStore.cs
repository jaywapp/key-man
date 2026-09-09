using System.Text.Json;
using KeyMan.Models;

namespace KeyMan.Services;

public sealed class MetadataStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public MetadataStore(string? filePath = null)
    {
        var dataDirectory = filePath is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMan")
            : Path.GetDirectoryName(filePath)
                ?? throw new ArgumentException("A file path with a directory is required.", nameof(filePath));
        Directory.CreateDirectory(dataDirectory);
        _filePath = filePath ?? Path.Combine(dataDirectory, "vault.json");
    }

    public async Task<IReadOnlyList<CredentialEntry>> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<CredentialEntry>>(stream, SerializerOptions) ?? [];
    }

    public async Task SaveAsync(IEnumerable<CredentialEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, entries, SerializerOptions);
        }

        File.Move(temporaryPath, _filePath, true);
    }
}
