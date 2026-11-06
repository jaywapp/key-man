using KeyMan.Mcp;
using KeyMan.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(_ => new MetadataStore(Environment.GetEnvironmentVariable("KEYMAN_VAULT_PATH")));
builder.Services.AddSingleton<WindowsCredentialStore>();
builder.Services.AddSingleton<IKeyManClipboard, WindowsClipboard>();
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "key-man-mcp",
            Version = typeof(KeyManTools).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion.Split('+')[0] ?? "1.1.0-alpha.1"
        };
        options.ServerInstructions = "KeyMan stores secrets in Windows Credential Manager. Never request, reveal, or return secret values. Use keyman_copy to place a secret on the local clipboard, and register only from the local clipboard or a named environment variable.";
    })
    .WithStdioServerTransport()
    .WithTools<KeyManTools>();

using var host = builder.Build();
await host.RunAsync();
