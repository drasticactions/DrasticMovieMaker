# Packages the desktop Windows app (NativeAOT, win-x64, Windows 10 1809+) as a portable zip:
# <out>\DrasticMovieMaker-<version>-win-x64.zip, holding the folder DrasticMovieMaker-<version>-win-x64.
# Run on Windows with the .NET SDK and Visual Studio Build Tools (NativeAOT links with their linker). The bundled
# FFmpeg comes from tools\ffmpeg\out\win-x64 (tools/ffmpeg/build-desktop.sh win-x64 under MSYS2 UCRT64, or the
# ffmpeg-desktop workflow's ffmpeg-win-x64-<recipe key> artifact). The zip writes nothing to the registry; README.txt
# explains Open With.
#
# Signing hook, unsigned by default: with AMM_WINDOWS_CERT (a .pfx path) and AMM_WINDOWS_CERT_PASSWORD set, signtool
# signs the exe and every DLL (SHA-256, RFC 3161 timestamp from AMM_WINDOWS_TIMESTAMP_URL, default DigiCert's).
# Unsigned, SmartScreen warns on the first run.
# Usage: pwsh tools/package-windows.ps1 [-Out <folder, default out\>]
param([string]$Out)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $Out) { $Out = Join-Path $root 'out' }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path $Out).Path

$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$ffmpeg = Join-Path $root 'tools\ffmpeg\out\win-x64'
if (-not (Test-Path (Join-Path $ffmpeg 'avformat-63.dll'))) {
    throw "FFmpeg for win-x64 is not built: run tools/ffmpeg/build-desktop.sh win-x64 in an MSYS2 UCRT64 shell, or download the ffmpeg-desktop workflow's ffmpeg-win-x64-<recipe key> artifact into $ffmpeg."
}

# NativeAOT finds the linker through vcvarsall.bat, which in newer Visual Studio runs vswhere.exe from PATH. CI images
# have it there; a desktop install keeps it in the Visual Studio Installer folder.
$installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (-not (Get-Command vswhere.exe -ErrorAction SilentlyContinue) -and (Test-Path (Join-Path $installer 'vswhere.exe'))) {
    $env:PATH = "$installer;$env:PATH"
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("amm-package-" + [Guid]::NewGuid().ToString('N'))
$name = "DrasticMovieMaker-$version-win-x64"
$folder = Join-Path $work $name
try {
    # The csproj copies the FFmpeg DLLs next to the executable.
    dotnet publish (Join-Path $root 'src\AvaMovieMaker') -c Release -r win-x64 -o (Join-Path $work 'publish') -warnaserror "-p:AmmFFmpegDir=$($ffmpeg.Replace('\', '/'))/"
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

    # The program and its native DLLs (symbols and .pdb files stay out).
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    Copy-Item (Join-Path $work 'publish\DrasticMovieMaker.exe') $folder
    Copy-Item (Join-Path $work 'publish\*.dll') $folder

    # Licenses, the FFmpeg build's configure line and sources, and the exact revision (the GPL source offer).
    Copy-Item (Join-Path $root 'LICENSE.md'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') $folder
    Copy-Item (Join-Path $ffmpeg 'BUILDINFO.txt') (Join-Path $folder 'FFMPEG-BUILDINFO.txt')
    $revision = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { (git -C $root rev-parse HEAD 2>$null) }
    if (-not $revision) { $revision = 'unknown' }
    $repo = if ($env:GITHUB_REPOSITORY) { " of $($env:GITHUB_SERVER_URL)/$($env:GITHUB_REPOSITORY)" } else { '' }
    @(
        "Drastic Movie Maker $version, licensed under the GNU GPL version 3 or later (LICENSE.md)."
        "Built from revision $revision$repo."
        "FFmpeg build (pinned sources, configure options): tools/ffmpeg (build-desktop.sh) in that revision;"
        "its configure line and source revisions are in FFMPEG-BUILDINFO.txt."
        "Third-party components and their licenses: THIRD-PARTY-NOTICES.md."
    ) | Set-Content -Encoding utf8 (Join-Path $folder 'SOURCE.txt')

    @(
        "Drastic Movie Maker $version"
        ""
        "https://github.com/drasticactions/drasticmoviemaker"
    ) | Set-Content -Encoding utf8 (Join-Path $folder 'README.txt')

    if ($env:AMM_WINDOWS_CERT -and $env:AMM_WINDOWS_CERT_PASSWORD) {
        $timestamp = if ($env:AMM_WINDOWS_TIMESTAMP_URL) { $env:AMM_WINDOWS_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }
        $files = @(Get-ChildItem $folder -Include *.exe, *.dll -Recurse | ForEach-Object FullName)
        signtool sign /fd sha256 /f $env:AMM_WINDOWS_CERT /p $env:AMM_WINDOWS_CERT_PASSWORD /tr $timestamp /td sha256 $files
        if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }
    } else {
        Write-Host 'AMM_WINDOWS_CERT is not set: the app is unsigned.'
    }

    $zip = Join-Path $Out "$name.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path $folder -DestinationPath $zip
    Write-Host $zip
}
finally {
    if (Test-Path $work) { Remove-Item -Recurse -Force $work }
}
