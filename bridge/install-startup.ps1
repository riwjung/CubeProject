# Publishes CubeBridge to %LOCALAPPDATA%\CubeBridge\app and starts it at Windows logon (tray icon).
#   powershell -ExecutionPolicy Bypass -File bridge\install-startup.ps1 -CubeHost 192.168.1.50
param(
    [string]$CubeHost = "cube.local",
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$appDir = Join-Path $env:LOCALAPPDATA "CubeBridge\app"
$shortcut = Join-Path ([Environment]::GetFolderPath("Startup")) "CubeBridge.lnk"

Get-Process CubeBridge -ErrorAction SilentlyContinue | Stop-Process

if ($Uninstall) {
    Remove-Item $shortcut -ErrorAction SilentlyContinue
    Remove-Item $appDir -Recurse -ErrorAction SilentlyContinue
    Write-Host "CubeBridge removed."
    return
}

dotnet publish (Join-Path $PSScriptRoot "CubeBridge") -c Release -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$settings = Join-Path $appDir "appsettings.json"
$json = Get-Content $settings -Raw | ConvertFrom-Json
$json.Cube.Host = $CubeHost
$json | ConvertTo-Json -Depth 5 | Set-Content $settings -Encoding utf8

$lnk = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
$lnk.TargetPath = Join-Path $appDir "CubeBridge.exe"
$lnk.WorkingDirectory = $appDir
$lnk.Save()

Start-Process $lnk.TargetPath -WorkingDirectory $appDir
Write-Host "CubeBridge installed (cube host: $CubeHost) and started. Look for the tray icon."
