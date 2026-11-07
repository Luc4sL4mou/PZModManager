# Gera o zip da Release do GitHub: exe único que já inclui o .NET (não precisa instalar nada).
# Uso: powershell -ExecutionPolicy Bypass -File release.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
[xml]$proj = Get-Content "$root\src\PZModManager.csproj"
$version = $proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$out = "$root\release"
$stage = "$out\PZModManager"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish "$root\src\PZModManager.csproj" -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $stage
if ($LASTEXITCODE -ne 0) { throw "Falha na compilação" }

# no zip vai só o exe (o resto, se houver, é intermediário do publish)
Get-ChildItem $stage | Where-Object Name -ne 'PZModManager.exe' | Remove-Item -Recurse -Force
# nome sem a versão: o link releases/latest/download/... do README continua valendo nas próximas versões
$zip = "$out\PZModManager-win-x64.zip"
Compress-Archive -Path $stage -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
"$hash  $(Split-Path $zip -Leaf)" | Set-Content "$zip.sha256" -Encoding ascii
Write-Host "OK: $zip (v$version)"
Write-Host "SHA-256: $hash"
