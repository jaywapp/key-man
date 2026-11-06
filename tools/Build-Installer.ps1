param(
    [string]$Version = '1.1.0-alpha.1',
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-alpha\.([1-9][0-9]*)$') {
    throw 'Version must have the form 1.1.0-alpha.1. Only explicit alpha versions can be packaged.'
}
$versionParts = @($Matches[1], $Matches[2], $Matches[3], $Matches[4])
foreach ($part in $versionParts) {
    $number = 0
    if (-not [int]::TryParse($part, [ref]$number) -or $number -gt 65535) {
        throw 'Each numeric version component must be between 0 and 65535.'
    }
}
$fileVersion = $versionParts -join '.'
# Review and update this pin when preparing a release with a newer supported runtime patch.
$runtimeFrameworkVersion = '10.0.14'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $IsccPath) {
    $isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($isccCommand) { $IsccPath = $isccCommand.Source }
    else {
        $candidates = @(
            (Join-Path $repositoryRoot 'artifacts\tooling\inno-6.7.4\ISCC.exe'),
            "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        )
        $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw 'Inno Setup ISCC.exe was not found. Install Inno Setup or supply -IsccPath.'
}

Push-Location -LiteralPath $repositoryRoot
try {
    $publishTargets = @(
        @{ Project = 'src\KeyMan\KeyMan.csproj'; Directory = 'gui' },
        @{ Project = 'src\KeyMan.Cli\KeyMan.Cli.csproj'; Directory = 'cli' },
        @{ Project = 'src\KeyMan.Mcp\KeyMan.Mcp.csproj'; Directory = 'mcp' }
    )
    foreach ($target in $publishTargets) {
        $publishDirectory = Join-Path $repositoryRoot "artifacts\publish\$Version\$($target.Directory)"
        & dotnet publish $target.Project -c Release -r win-x64 --self-contained true `
            "-p:Version=$Version" "-p:FileVersion=$fileVersion" `
            "-p:RuntimeFrameworkVersion=$runtimeFrameworkVersion" `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true -o $publishDirectory
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($target.Project)" }
    }

    $outputDirectory = Join-Path $repositoryRoot 'artifacts\installer'
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    $guiDirectory = Join-Path $repositoryRoot "artifacts\publish\$Version\gui"
    $cliDirectory = Join-Path $repositoryRoot "artifacts\publish\$Version\cli"
    $mcpDirectory = Join-Path $repositoryRoot "artifacts\publish\$Version\mcp"
    $noticesDirectory = Join-Path $repositoryRoot "artifacts\publish\$Version\notices"
    & (Join-Path $PSScriptRoot 'Write-ThirdPartyNotices.ps1') -OutputDirectory $noticesDirectory -RuntimeFrameworkVersion $runtimeFrameworkVersion
    & $IsccPath "/DMyAppVersion=$Version" "/DMyAppFileVersion=$fileVersion" `
        "/DGuiSourceDir=$guiDirectory" "/DCliSourceDir=$cliDirectory" `
        "/DMcpSourceDir=$mcpDirectory" "/DNoticesSourceDir=$noticesDirectory" "/DInstallerOutputDir=$outputDirectory" .\installer\KeyMan.iss
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

    $installerPath = Join-Path $outputDirectory "KeyMan-Setup-$Version.exe"
    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) { throw 'The installer was not created.' }
    $checksumPath = "$installerPath.sha256"
    $checksum = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
    $checksumLine = '{0}  {1}' -f $checksum.Hash.ToLowerInvariant(), [IO.Path]::GetFileName($installerPath)
    [IO.File]::WriteAllText($checksumPath, $checksumLine + [Environment]::NewLine, [Text.Encoding]::ASCII)
    Write-Output "Installer: $installerPath"
    Write-Output "SHA-256: $checksumPath"
}
finally {
    Pop-Location
}
