param(
    [ValidateSet('region_a', 'region_b')][string]$Region = 'region_a',
    [string]$ApplicationId = 'com.mrtwin.inspector',
    [string]$Adb = 'E:\Unity\Editor\6000.3.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe',
    [string]$Serial = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root "work\twin\HGE_2022-02-06\$Region"
$manifestPath = Join-Path $source 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Manifest missing: $manifestPath" }
if ($ApplicationId -notmatch '^[a-zA-Z0-9_.]+$') { throw 'Invalid application id' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$adbArguments = @()
if ($Serial) { $adbArguments = @('-s', $Serial) }
& $Adb @adbArguments get-state
if ($LASTEXITCODE -ne 0) { throw 'Connect one authorized headset, or pass -Serial.' }
$destination = "/sdcard/Android/data/$ApplicationId/files/twin/HGE_2022-02-06"
$revision = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant()
$versionDestination = "$destination/versions/$revision"
& $Adb @adbArguments shell mkdir -p $versionDestination
if ($LASTEXITCODE -ne 0) { throw 'Cannot create device data directory. Install and launch the app first.' }
# Push only runtime files, never multi-gigabyte processing intermediates.
& $Adb @adbArguments push --sync (Join-Path $source 'tiles') "$versionDestination/"
if ($LASTEXITCODE -ne 0) { throw 'Tile transfer failed; previous manifest retained.' }
& $Adb @adbArguments push (Join-Path $PSScriptRoot 'markers\markers.json') "$destination/markers.json"
if ($LASTEXITCODE -ne 0) { throw 'Marker transfer failed.' }
# Versioned paths preserve the old dataset during an interrupted transfer.
foreach ($tile in $manifest.tiles) {
    foreach ($entry in $tile.files.PSObject.Properties) {
        $entry.Value = "versions/$revision/$($entry.Value)"
    }
}
$staging = Join-Path $root 'work\tmp\deploy'
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$deviceManifest = Join-Path $staging "manifest-$revision.json"
$manifest | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $deviceManifest -Encoding utf8NoBOM
& $Adb @adbArguments push $deviceManifest "$destination/manifest.json.pending"
if ($LASTEXITCODE -ne 0) { throw 'Manifest transfer failed.' }
& $Adb @adbArguments shell mv "$destination/manifest.json.pending" "$destination/manifest.json"
if ($LASTEXITCODE -ne 0) { throw 'Manifest activation failed.' }
Write-Host "Pushed $Region ($($manifest.tiles.Count) tiles). Restart TwinInspector to load it."
