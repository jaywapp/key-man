param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,
    [switch]$RegisterEnvironment
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$installer = Get-Item -LiteralPath $InstallerPath -ErrorAction Stop
$checksumPath = $installer.FullName + '.sha256'
$expectedHash = (Get-Content -LiteralPath $checksumPath -Raw).Split(' ')[0]
$actualHash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash
if ($expectedHash -notmatch '^[0-9a-fA-F]{64}$' -or $actualHash -ne $expectedHash) {
    throw 'The installer SHA-256 checksum did not match.'
}
$pathBefore = [Environment]::GetEnvironmentVariable('Path', 'User')
$homeBefore = [Environment]::GetEnvironmentVariable('KEYMAN_HOME', 'User')
if ($RegisterEnvironment -and ($homeBefore -or (Test-Path -LiteralPath 'HKCU:\Software\KeyMan'))) {
    throw 'Registration tests require a clean Windows user without an existing KeyMan installation.'
}
$testDirectory = Join-Path $repositoryRoot ('artifacts\install-tests\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $testDirectory | Out-Null
$arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/NOICONS', ('/DIR="' + $testDirectory + '"'))
if ($RegisterEnvironment) { $arguments += '/TASKS=addtopath' }
else { $arguments += '/ISOLATEDTEST=1'; $arguments += '/TASKS=' }

$serverBefore = [Environment]::GetEnvironmentVariable('KEYMAN_MCP_TEST_SERVER', 'Process')
$vaultBefore = [Environment]::GetEnvironmentVariable('KEYMAN_VAULT_PATH', 'Process')
$fixtureName = 'Packaging-' + [Guid]::NewGuid().ToString('N')
$fixtureValue = 'keyman-packaging-' + [Guid]::NewGuid().ToString('N')
$cliExecutable = Join-Path $testDirectory 'key-man.exe'
$fixtureRegistered = $false
Push-Location -LiteralPath $repositoryRoot
try {
    $env:KEYMAN_VAULT_PATH = Join-Path $testDirectory 'test-data\vault.json'
    $process = Start-Process -FilePath $installer.FullName -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Installer exited with code $($process.ExitCode)." }
    foreach ($file in @('key-man.exe', 'keyman.exe', 'key-man-mcp.exe', 'KeyMan.Gui.exe', 'unins000.exe', 'LICENSE.txt', 'ThirdPartyNotices\packages.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $testDirectory $file) -PathType Leaf)) {
            throw "Installed file was not found: $file"
        }
    }
    $sourceLicenseHash = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Algorithm SHA256).Hash
    $installedLicenseHash = (Get-FileHash -LiteralPath (Join-Path $testDirectory 'LICENSE.txt') -Algorithm SHA256).Hash
    if ($sourceLicenseHash -ne $installedLicenseHash) { throw 'The installed license does not match the repository license.' }
    & $cliExecutable --help | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The installed CLI did not run.' }

    $fixtureValue | & $cliExecutable register $fixtureName --stdin --service Packaging --environment Test | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The installed CLI could not register a fixture.' }
    $fixtureRegistered = $true
    $fixtureList = & $cliExecutable list --json
    if ($LASTEXITCODE -ne 0) { throw 'The installed CLI could not list the fixture.' }
    $fixtureEntries = @($fixtureList | ConvertFrom-Json)
    if ($fixtureEntries.Count -ne 1 -or $fixtureEntries[0].Name -ne $fixtureName) {
        throw 'The installed CLI returned unexpected fixture metadata.'
    }
    $fixtureRead = & $cliExecutable get $fixtureName --service Packaging --environment Test
    if ($LASTEXITCODE -ne 0 -or $fixtureRead -cne $fixtureValue) {
        throw 'The installed CLI did not round-trip the fixture value.'
    }
    & $cliExecutable remove $fixtureName --service Packaging --environment Test --yes | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The installed CLI could not remove the fixture.' }
    $fixtureRegistered = $false

    $env:KEYMAN_MCP_TEST_SERVER = Join-Path $testDirectory 'key-man-mcp.exe'
    & dotnet run --project .\tests\KeyMan.McpTests\KeyMan.McpTests.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'The installed MCP server failed its protocol smoke test.' }

    $pathAfter = [Environment]::GetEnvironmentVariable('Path', 'User')
    $homeAfter = [Environment]::GetEnvironmentVariable('KEYMAN_HOME', 'User')
    if ($RegisterEnvironment) {
        if (($pathAfter -split ';') -notcontains $testDirectory -or $homeAfter -ne $testDirectory) {
            throw 'The installer did not register the user environment.'
        }
    }
    elseif ($pathBefore -cne $pathAfter -or $homeBefore -cne $homeAfter) {
        throw 'The isolated installer changed the user environment.'
    }
}
finally {
    try {
        if ($fixtureRegistered -and (Test-Path -LiteralPath $cliExecutable)) {
            & $cliExecutable remove $fixtureName --service Packaging --environment Test --yes | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Fixture cleanup failed; test data was preserved for recovery.' }
        }
        $uninstaller = Join-Path $testDirectory 'unins000.exe'
        if (Test-Path -LiteralPath $uninstaller) {
            $uninstallProcess = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -WindowStyle Hidden -Wait -PassThru
            if ($uninstallProcess.ExitCode -ne 0) { throw "Uninstaller exited with code $($uninstallProcess.ExitCode)." }
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable('KEYMAN_MCP_TEST_SERVER', $serverBefore, 'Process')
        [Environment]::SetEnvironmentVariable('KEYMAN_VAULT_PATH', $vaultBefore, 'Process')
        Pop-Location
    }
}
$pathAfterUninstall = [Environment]::GetEnvironmentVariable('Path', 'User')
$homeAfterUninstall = [Environment]::GetEnvironmentVariable('KEYMAN_HOME', 'User')
if ($RegisterEnvironment) {
    if (($pathAfterUninstall -split ';') -contains $testDirectory -or $homeAfterUninstall) {
        throw 'The uninstaller left the test environment registered.'
    }
}
elseif ($pathBefore -cne $pathAfterUninstall -or $homeBefore -cne $homeAfterUninstall) {
    throw 'The isolated uninstaller changed the user environment.'
}
if (Test-Path -LiteralPath (Join-Path $testDirectory 'key-man.exe')) {
    throw 'The uninstaller left the installed CLI behind.'
}
Write-Output 'PASS: installer checksum, license integrity, packaged CLI fixture round trip, MCP initialization, environment, and uninstall'
