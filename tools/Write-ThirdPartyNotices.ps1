param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [string]$RuntimeFrameworkVersion = '10.0.14'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$upstreamDirectory = Join-Path $repositoryRoot 'installer\upstream-licenses'
$upstreamSources = Get-Content -LiteralPath (Join-Path $upstreamDirectory 'sources.json') -Raw | ConvertFrom-Json
$packages = @{}
$packageFolders = @{}
foreach ($project in 'KeyMan', 'KeyMan.Cli', 'KeyMan.Mcp') {
    $assetPath = Join-Path $repositoryRoot "src\$project\obj\project.assets.json"
    $assets = Get-Content -LiteralPath $assetPath -Raw | ConvertFrom-Json
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) { $packageFolders[$folder] = $true }
    foreach ($target in $assets.targets.PSObject.Properties) {
        foreach ($library in $target.Value.PSObject.Properties) {
            if ($library.Value.type -eq 'package' -and ($library.Value.runtime -or $library.Value.runtimeTargets)) {
                $packages[$library.Name] = $true
            }
        }
    }
}
foreach ($runtimePack in 'Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64') {
    $packages["$runtimePack/$RuntimeFrameworkVersion"] = $true
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$manifest = @()
foreach ($identity in $packages.Keys | Sort-Object) {
    $parts = $identity -split '/'
    $id = $parts[0]
    $version = $parts[1]
    if ($id -notmatch '^(Microsoft\.|ModelContextProtocol)' -or $id -notmatch '^[A-Za-z0-9_.-]+$' -or
        $version -notmatch '^[A-Za-z0-9_.-]+$') {
        throw "Review the new dependency before packaging attribution: $identity"
    }
    $packageDirectory = $null
    foreach ($folder in $packageFolders.Keys) {
        $candidate = Join-Path $folder ($identity.ToLowerInvariant())
        if (Test-Path -LiteralPath $candidate -PathType Container) { $packageDirectory = $candidate; break }
    }
    if (-not $packageDirectory) { throw "Resolved package was not found: $identity" }
    $nuspec = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' -File | Select-Object -First 1
    [xml]$specification = Get-Content -LiteralPath $nuspec.FullName -Raw
    $metadata = $specification.package.metadata
    $destination = Join-Path $OutputDirectory "$id-$version"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -LiteralPath $nuspec.FullName -Destination $destination
    $noticeFiles = @(Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object {
        $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE|THIRD.PARTY.NOTICES)([.-].*)?$'
    })
    foreach ($notice in $noticeFiles) { Copy-Item -LiteralPath $notice.FullName -Destination $destination }
    $licenseSource = 'NuGet package contents'
    $noticeSource = 'NuGet package contents, when present'
    if (-not ($noticeFiles | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING)([.-].*)?$' })) {
        $upstream = @($upstreamSources | Where-Object {
            $_.repository -eq $metadata.repository.url -and $_.commit -eq $metadata.repository.commit
        })
        if ($upstream.Count -ne 1) { throw "An exact upstream license must be reviewed for $identity" }
        Copy-Item -LiteralPath (Join-Path $upstreamDirectory $upstream[0].file) -Destination (Join-Path $destination 'LICENSE.upstream')
        $licenseSource = $upstream[0].source
        if ($upstream[0].noticeFile) {
            Copy-Item -LiteralPath (Join-Path $upstreamDirectory $upstream[0].noticeFile) -Destination (Join-Path $destination 'THIRD-PARTY-NOTICES.upstream')
            $noticeSource = $upstream[0].noticeSource
        }
    }
    $manifest += [pscustomobject]@{
        package = $id
        version = $version
        source = "https://www.nuget.org/packages/$id/$version"
        repository = [string]$metadata.repository.url
        commit = [string]$metadata.repository.commit
        license = [string]$metadata.license.InnerText
        licenseSource = $licenseSource
        noticeSource = $noticeSource
        directory = "$id-$version"
        files = @(Get-ChildItem -LiteralPath $destination -File | Select-Object -ExpandProperty Name)
    }
}
$manifestPath = Join-Path $OutputDirectory 'packages.json'
$manifestJson = ConvertTo-Json -InputObject @($manifest) -Depth 6
[IO.File]::WriteAllText($manifestPath, $manifestJson, (New-Object Text.UTF8Encoding($false)))
$readme = "Third-party dependency notices for the self-contained KeyMan Windows package.`r`n" +
    "packages.json maps resolved runtime dependencies and runtime packs to their versions, upstream sources, and original license/notice files.`r`n" +
    "Package nuspec files preserve their original package metadata and copyright statements. Upstream texts are copied without modification.`r`n" +
    "These notices apply to third-party components and do not assign a license to KeyMan itself.`r`n"
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'README.txt'), $readme, (New-Object Text.UTF8Encoding($false)))
Write-Output "Prepared third-party attribution for $($manifest.Count) runtime packages/packs."
