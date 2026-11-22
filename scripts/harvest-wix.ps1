$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root "artifacts\app"
$out = Join-Path $root "packaging\wix\Components.wxs"
$files = Get-ChildItem $app -File | Sort-Object Name

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <ComponentGroup Id="AppFiles">')
foreach ($file in $files) {
    $relative = $file.FullName.Substring($root.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    [void]$sb.AppendLine('      <Component Directory="INSTALLFOLDER" Guid="*">')
    [void]$sb.AppendLine('        <File Source="' + $relative + '" />')
    [void]$sb.AppendLine('      </Component>')
}
[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')
Set-Content -Path $out -Value $sb.ToString() -Encoding utf8
Write-Host ("Components written for " + $files.Count + " files")
