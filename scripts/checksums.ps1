$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"

$lines = Get-ChildItem $dist -File |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object Name |
    ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + "  " + $_.Name }

$text = ($lines -join "`n") + "`n"
[System.IO.File]::WriteAllText((Join-Path $dist "SHA256SUMS.txt"), $text, [System.Text.Encoding]::ASCII)
Write-Host "Wrote $($lines.Count) checksums with LF line endings."
