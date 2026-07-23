# KeyMan MCP server

`key-man-mcp.exe` is a local stdio MCP server for Codex and Claude Code. It uses the same Windows Credential Manager vault as the KeyMan GUI and CLI.

## Security boundary

The MCP server never returns a secret value in tool results. Secret values move only between local sources and Windows Credential Manager:

- `keyman_list`: returns non-sensitive metadata only.
- `keyman_copy`: copies a stored secret to the local Windows clipboard and returns confirmation only.
- `keyman_register_from_clipboard`: stores the current local clipboard text.
- `keyman_register_from_environment`: stores the value of a named local environment variable.
- `keyman_remove`: permanently removes a credential after the client confirms the destructive tool call.

Do not put a secret directly in an AI prompt or MCP argument. Put it on the local clipboard or in a temporary environment variable instead.

## Run from source

```powershell
dotnet run --project .\src\KeyMan.Mcp\KeyMan.Mcp.csproj
```

For isolated tests, set `KEYMAN_VAULT_PATH` to an alternate metadata JSON path. This changes only the non-sensitive metadata file location.

## Codex configuration

After installing KeyMan, register the stdio server with the Codex CLI:

```powershell
codex mcp add key-man -- key-man-mcp.exe
```

The marketplace plugin provides the equivalent configuration automatically.

## Claude Code configuration

Add this project-scoped `.mcp.json`, or install the marketplace plugin:

```json
{
  "mcpServers": {
    "key-man": {
      "command": "key-man-mcp.exe",
      "args": []
    }
  }
}
```

## Example requests

- "List my KeyMan entries for GitHub."
- "Copy the Production GitHub token from KeyMan."
- "Register the current clipboard as `Sample`, service `Demo`, environment `Development`."
- "Register `OPENAI_API_KEY` from the environment as `OpenAI`."
