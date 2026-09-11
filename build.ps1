[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "NowPlaying.exe")
)

$ErrorActionPreference = "Stop"

$frameworkDirectory = if (Test-Path (Join-Path $env:WINDIR "Microsoft.NET\\Framework64\\v4.0.30319")) {
    Join-Path $env:WINDIR "Microsoft.NET\\Framework64\\v4.0.30319"
} else {
    Join-Path $env:WINDIR "Microsoft.NET\\Framework\\v4.0.30319"
}

$compiler = Join-Path $frameworkDirectory "csc.exe"
$winmdCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\\10\\UnionMetadata"),
    (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\\10\\References")
)

$windowsMetadata = Get-ChildItem -Path $winmdCandidates -Filter "Windows.winmd" -File -Recurse -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if ($null -eq $windowsMetadata) {
    throw "Windows SDK metadata was not found. Install the Windows 10/11 SDK to build from source."
}

& $compiler /nologo /target:winexe /optimize+ /debug- /platform:anycpu `
    "/out:$OutputPath" `
    "/reference:$frameworkDirectory\\System.Runtime.dll" `
    "/reference:$frameworkDirectory\\System.Runtime.WindowsRuntime.dll" `
    "/reference:$frameworkDirectory\\WPF\\PresentationCore.dll" `
    "/reference:$frameworkDirectory\\WPF\\PresentationFramework.dll" `
    "/reference:$frameworkDirectory\\WPF\\WindowsBase.dll" `
    "/reference:$frameworkDirectory\\System.Xaml.dll" `
    "/reference:$($windowsMetadata.FullName)" `
    (Join-Path $PSScriptRoot "SpotifyNow.cs")

if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

Write-Host "Built $OutputPath"
