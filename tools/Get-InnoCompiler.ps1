$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$toolDirectory = Join-Path $repositoryRoot 'artifacts\tooling'
$compilerDirectory = Join-Path $toolDirectory 'inno-6.7.4'
$installerPath = Join-Path $toolDirectory 'innosetup-6.7.4.exe'
$expectedHash = 'A549929319F9120716677C685535CA4671E4B937B5FDC87E2585F32D8EB30E8C'
New-Item -ItemType Directory -Force -Path $toolDirectory | Out-Null
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_4/innosetup-6.7.4.exe' -OutFile $installerPath
}
$downloadHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
$signature = Get-AuthenticodeSignature -LiteralPath $installerPath
if ($downloadHash -ne $expectedHash -or $signature.Status -ne 'Valid' -or
    $signature.SignerCertificate.Subject -notmatch '(^|, )O=Pyrsys B\.V\.(,|$)') {
    throw 'The official Inno Setup download failed SHA-256 or publisher signature verification.'
}
$compilerPath = Join-Path $compilerDirectory 'ISCC.exe'
if (-not (Test-Path -LiteralPath $compilerPath -PathType Leaf)) {
    $arguments = @('/PORTABLE=1', '/CURRENTUSER', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/TASKS=', ('/DIR="' + $compilerDirectory + '"'))
    $process = Start-Process -FilePath $installerPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw 'Portable Inno Setup extraction failed.' }
}
if (-not (Test-Path -LiteralPath $compilerPath -PathType Leaf)) { throw 'ISCC.exe was not extracted.' }
Write-Output $compilerPath
