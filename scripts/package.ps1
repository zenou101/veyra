param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") { "$env:USERPROFILE\.dotnet\dotnet.exe" } else { "dotnet" }
$app = Join-Path $root "artifacts\app"
$guard = Join-Path $root "artifacts\guard"
$dist = Join-Path $root "dist"

Write-Host "Publishing guard and application ($Configuration / $Runtime)..."
# Publish into empty folders: leftover files from an earlier publish can mix runtime versions.
if (Test-Path $app) { Remove-Item $app -Recurse -Force }
if (Test-Path $guard) { Remove-Item $guard -Recurse -Force }
New-Item -ItemType Directory -Force -Path $app, $guard, $dist | Out-Null

& $dotnet publish (Join-Path $root "src\Veyra.DisplayGuard\Veyra.DisplayGuard.csproj") -c $Configuration -r $Runtime --self-contained true -o $guard
if ($LASTEXITCODE -ne 0) { throw "Guard publish failed." }

& $dotnet publish (Join-Path $root "src\Veyra.UI\Veyra.UI.csproj") -c $Configuration -r $Runtime --self-contained true -o $app
if ($LASTEXITCODE -ne 0) { throw "Application publish failed." }

foreach ($file in @("Veyra.DisplayGuard.exe", "Veyra.DisplayGuard.dll", "Veyra.DisplayGuard.deps.json", "Veyra.DisplayGuard.runtimeconfig.json")) {
    Copy-Item (Join-Path $guard $file) (Join-Path $app $file) -Force
}

$zip = Join-Path $dist "Veyra-Portable-$Version-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $app "*") -DestinationPath $zip -CompressionLevel Optimal

$hashes = Get-ChildItem $dist -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" } | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
}
$text = ($hashes -join "`n") + "`n"
[System.IO.File]::WriteAllText((Join-Path $dist "SHA256SUMS.txt"), $text, [System.Text.Encoding]::ASCII)

Write-Host "Artifacts:"
Get-ChildItem $dist | Select-Object Name, Length
