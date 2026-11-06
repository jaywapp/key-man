param(
    [string]$RootPath = 'D:\workspace',
    [string]$KeyManPath,
    [string]$VaultPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) {
    throw 'RootPath must be an existing directory.'
}

if ([string]::IsNullOrWhiteSpace($KeyManPath)) {
    $command = Get-Command 'key-man.exe', 'keyman.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $command) {
        throw 'Install KeyMan or specify -KeyManPath with the built CLI executable.'
    }
    $KeyManPath = $command.Source
}

$KeyManPath = (Resolve-Path -LiteralPath $KeyManPath).ProviderPath
if (-not (Test-Path -LiteralPath $KeyManPath -PathType Leaf) -or
    [IO.Path]::GetExtension($KeyManPath) -ne '.exe') {
    throw 'KeyManPath must point to the KeyMan CLI executable.'
}

# Encode arguments for Windows CommandLineToArgvW parsing, including trailing backslashes.
function ConvertTo-NativeArgument {
    param([string]$Value)
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Register-KeyManEntry {
    param([string]$Name, [string]$Service, [string]$Value, [string]$SourcePath)

    $arguments = @('register', $Name, '--stdin', '--skip-existing', '--service', $Service,
        '--environment', 'Development', '--notes', ('Imported from ' + $SourcePath))
    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = $KeyManPath
    $startInfo.Arguments = ($arguments | ForEach-Object { ConvertTo-NativeArgument $_ }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    if (-not [string]::IsNullOrWhiteSpace($VaultPath)) {
        $startInfo.EnvironmentVariables['KEYMAN_VAULT_PATH'] = [IO.Path]::GetFullPath($VaultPath)
    }

    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        [void]$process.Start()
        $outputTask = $process.StandardOutput.ReadToEndAsync()
        $errorTask = $process.StandardError.ReadToEndAsync()
        $inputBytes = [Text.Encoding]::UTF8.GetBytes($Value)
        $process.StandardInput.BaseStream.Write($inputBytes, 0, $inputBytes.Length)
        $process.StandardInput.Close()
        $process.WaitForExit()
        $output = $outputTask.GetAwaiter().GetResult()
        [void]$errorTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "KeyMan registration failed for '$Name' (exit code $($process.ExitCode)). Earlier successful entries are preserved."
        }

        return $output.StartsWith('Skipped existing:', [StringComparison]::Ordinal)
    }
    finally {
        $process.Dispose()
    }
}

$imported = 0
$skippedDuplicates = 0
$skippedInvalid = 0
$envFiles = Get-ChildItem -LiteralPath $RootPath -Recurse -Force -File -Filter '.env' -ErrorAction Stop |
    Where-Object { $_.FullName -notmatch '\\(node_modules|bin|obj|dist|build|\.git)\\' }

foreach ($envFile in $envFiles) {
    $service = Split-Path $envFile.DirectoryName -Leaf
    foreach ($line in Get-Content -LiteralPath $envFile.FullName -Encoding UTF8) {
        if ($line -notmatch '^\s*(?:export\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=(?<value>.*)$') {
            continue
        }

        $name = $Matches.name
        $value = $Matches.value.Trim()
        if ($value.Length -ge 2 -and
            (($value.StartsWith('"') -and $value.EndsWith('"')) -or
             ($value.StartsWith("'") -and $value.EndsWith("'")))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        if ([string]::IsNullOrWhiteSpace($value) -or
            $value -match '^(?:your_|replace_|change_me|todo|example|placeholder|xxx)' -or
            [Text.Encoding]::Unicode.GetByteCount($value) -gt 2560) {
            $skippedInvalid++
            continue
        }

        if (Register-KeyManEntry -Name $name -Service $service -Value $value -SourcePath $envFile.FullName) {
            $skippedDuplicates++
        }
        else {
            $imported++
        }
    }
}

Write-Output ('Imported entries: ' + $imported)
Write-Output ('Skipped duplicates: ' + $skippedDuplicates)
Write-Output ('Skipped empty, placeholder, or oversized values: ' + $skippedInvalid)
