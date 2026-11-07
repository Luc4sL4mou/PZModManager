# Compila o PZ Mod Manager e atualiza o exe da raiz (o atalho da área de trabalho aponta para ele).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Get-Process PZModManager -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet publish "$root\src\PZModManager.csproj" -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o "$root\publish"
if ($LASTEXITCODE -ne 0) { throw "Falha na compilação" }
Copy-Item "$root\publish\PZModManager.exe" "$root\PZModManager.exe" -Force
Write-Host "OK: $root\PZModManager.exe"
