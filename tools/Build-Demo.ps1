param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$frameDirectory = Join-Path $repositoryRoot 'artifacts\demo\frames'
$mediaDirectory = Join-Path $repositoryRoot 'docs\assets\demo'
foreach ($command in @('dotnet', 'ffmpeg', 'ffprobe')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) { throw "Required command was not found: $command" }
}
Push-Location -LiteralPath $repositoryRoot
try {
    & dotnet build .\tools\KeyMan.Demo\KeyMan.Demo.csproj -c Release --nologo -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Demo tool build failed.' }
    & dotnet build .\src\KeyMan.Cli\KeyMan.Cli.csproj -c Release --nologo -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Read-only CLI fixture build failed.' }
    & dotnet run --project .\tools\KeyMan.Demo\KeyMan.Demo.csproj -c Release --no-build -- $repositoryRoot $frameDirectory $mediaDirectory
    if ($LASTEXITCODE -ne 0) { throw 'WPF fixture rendering failed.' }

    $framePattern = Join-Path $frameDirectory 'frame-%04d.png'
    $video = Join-Path $mediaDirectory 'keyman-threads-square.mp4'
    $gif = Join-Path $mediaDirectory 'keyman-demo.gif'
    & ffmpeg -hide_banner -loglevel warning -y -framerate 4 -start_number 1 -i $framePattern -t 26 `
        -c:v libx264 -pix_fmt yuv420p -r 30 -crf 22 -preset medium -movflags +faststart -an $video
    if ($LASTEXITCODE -ne 0) { throw 'MP4 encoding failed.' }
    & ffmpeg -hide_banner -loglevel warning -y -framerate 4 -start_number 1 -i $framePattern -t 26 `
        -filter_complex 'fps=4,scale=900:900:flags=lanczos,split[a][b];[a]palettegen=max_colors=96:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4' -loop 0 $gif
    if ($LASTEXITCODE -ne 0) { throw 'GIF encoding failed.' }

    $reports = @()
    foreach ($file in @($video, $gif)) {
        $probe = & ffprobe -v error -select_streams v:0 -show_entries 'stream=codec_name,width,height,pix_fmt,r_frame_rate,nb_frames:format=duration,size' -of json $file
        if ($LASTEXITCODE -ne 0) { throw 'Encoded media inspection failed.' }
        $report = $probe | ConvertFrom-Json
        if ([double]$report.format.duration -lt 15 -or [double]$report.format.duration -gt 30) { throw 'Demo duration is outside 15-30 seconds.' }
        $reports += [pscustomobject]@{ file = [IO.Path]::GetFileName($file); inspection = $report }
    }
    $json = ConvertTo-Json -InputObject $reports -Depth 8
    [IO.File]::WriteAllText((Join-Path $mediaDirectory 'media-verification.json'), $json, (New-Object Text.UTF8Encoding($false)))
    Write-Output 'PASS: 26-second GIF and square MP4 encoded and inspected.'
}
finally { Pop-Location }
