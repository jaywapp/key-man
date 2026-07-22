param(
    [Parameter(Mandatory = $false)]
    [string]$RootPath = 'D:\workspace'
)

$ErrorActionPreference = 'Stop'

$nativeSource = @'
using System;
using System.Runtime.InteropServices;

public static class KeyManCredentialNative
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct Credential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredDelete(string target, int type, int flags);
}
'@

Add-Type -TypeDefinition $nativeSource

function Write-KeyManCredential {
    param(
        [Parameter(Mandatory = $true)][string]$Target,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $blobSize = [Text.Encoding]::Unicode.GetByteCount($Value)
    if ($blobSize -gt 2560) {
        return $false
    }

    $pointer = [Runtime.InteropServices.Marshal]::StringToCoTaskMemUni($Value)
    try {
        $credential = New-Object KeyManCredentialNative+Credential
        $credential.Type = 1
        $credential.TargetName = $Target
        $credential.CredentialBlobSize = $blobSize
        $credential.CredentialBlob = $pointer
        $credential.Persist = 2
        $credential.UserName = 'KeyMan'

        if (-not [KeyManCredentialNative]::CredWrite([ref]$credential, 0)) {
            throw New-Object ComponentModel.Win32Exception(
                [Runtime.InteropServices.Marshal]::GetLastWin32Error())
        }

        return $true
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeCoTaskMemUnicode($pointer)
    }
}

$vaultDirectory = Join-Path $env:LOCALAPPDATA 'KeyMan'
$vaultPath = Join-Path $vaultDirectory 'vault.json'
New-Item -ItemType Directory -Force -Path $vaultDirectory | Out-Null

$entries = @()
if (Test-Path -LiteralPath $vaultPath) {
    $parsedEntries = Get-Content -LiteralPath $vaultPath -Encoding UTF8 -Raw | ConvertFrom-Json
    if ($null -ne $parsedEntries) {
        $entries = @($parsedEntries)
    }
}

$createdTargets = New-Object System.Collections.Generic.List[string]
$imported = New-Object System.Collections.Generic.List[object]
$skippedDuplicates = 0
$skippedInvalid = 0

try {
    $envFiles = Get-ChildItem -LiteralPath $RootPath -Recurse -Force -File -Filter '.env' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(node_modules|bin|obj|dist|build|\.git)\\' }

    foreach ($envFile in $envFiles) {
        if ($envFile.DirectoryName -eq 'D:\workspace\repositories') {
            $service = 'workspace'
        }
        else {
            $service = Split-Path $envFile.DirectoryName -Leaf
        }

        foreach ($line in Get-Content -LiteralPath $envFile.FullName -Encoding UTF8) {
            if ($line -notmatch '^\s*(?:export\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=(?<value>.*)$') {
                continue
            }

            $name = $Matches.name
            $value = $Matches.value.Trim()
            if ([string]::IsNullOrWhiteSpace($value) -or
                $value -match '^(?:"|'')?(?:your_|replace_|change_me|todo|example|placeholder|xxx)') {
                $skippedInvalid++
                continue
            }

            if ($value.Length -ge 2 -and
                (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                 ($value.StartsWith("'") -and $value.EndsWith("'")))) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            $duplicate = @($entries) |
                Where-Object {
                    $_.Name -eq $name -and
                    $_.Service -eq $service -and
                    $_.Environment -eq 'Development'
                } |
                Select-Object -First 1
            if ($null -ne $duplicate) {
                $skippedDuplicates++
                continue
            }

            $id = [Guid]::NewGuid()
            $target = 'KeyMan/' + $id.ToString('N')
            if (-not (Write-KeyManCredential -Target $target -Value $value)) {
                $skippedInvalid++
                continue
            }

            $createdTargets.Add($target)
            $entry = [pscustomobject][ordered]@{
                Id = $id.ToString()
                Name = $name
                Service = $service
                Environment = 'Development'
                Notes = 'Imported from ' + $envFile.FullName
                ExpiresOn = $null
                CreatedAt = [DateTimeOffset]::Now.ToString('o')
            }
            $entries += $entry
            $imported.Add([pscustomobject]@{ Service = $service; Variable = $name })
        }
    }

    $temporaryPath = $vaultPath + '.tmp'
    $json = $entries | ConvertTo-Json -Depth 6
    $utf8WithoutBom = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($temporaryPath, $json, $utf8WithoutBom)
    Move-Item -LiteralPath $temporaryPath -Destination $vaultPath -Force
}
catch {
    foreach ($target in $createdTargets) {
        [void][KeyManCredentialNative]::CredDelete($target, 1, 0)
    }
    throw
}

Write-Output ('Imported entries: ' + $imported.Count)
Write-Output ('Skipped duplicates: ' + $skippedDuplicates)
Write-Output ('Skipped empty, placeholder, or oversized values: ' + $skippedInvalid)
$imported | Sort-Object Service, Variable | Format-Table -AutoSize
