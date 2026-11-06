param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repositoryRoot
try {
    if (-not $NoBuild) {
        & dotnet build .\KeyMan.sln -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw 'The solution build failed.' }
    }

    $projects = @(
        'tests\RegressionTests\RegressionTests.csproj',
        'tests\KeyMan.ClipboardTests\KeyMan.ClipboardTests.csproj',
        'tests\KeyMan.UiTests\KeyMan.UiTests.csproj',
        'tests\KeyMan.SmokeTests\KeyMan.SmokeTests.csproj',
        'tests\KeyMan.McpTests\KeyMan.McpTests.csproj'
    )
    foreach ($project in $projects) {
        & dotnet run --project $project -c $Configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "Test project failed: $project" }
    }
}
finally {
    Pop-Location
}
