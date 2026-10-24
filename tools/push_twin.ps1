param(
    # Space id from TwinInspector/Assets/StreamingAssets/twins/spaces.json; data comes from work\twin\<Space>.
    [ValidatePattern('^[A-Za-z0-9_.-]+$')][string]$Space = 'HG_MainHall',
    [string]$ApplicationId = 'com.mrtwin.inspector',
    [string]$Adb = 'E:\Unity\Editor\6000.3.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe',
    [string]$Serial = '',
    [switch]$Models
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($Models) {
    if ($ApplicationId -notmatch '^[a-zA-Z0-9_.]+$') { throw 'Invalid application id' }
    $modelRoot = Join-Path $PSScriptRoot 'sam/models'
    $modelManifest = Get-Content -LiteralPath (Join-Path $modelRoot 'models.json') -Raw | ConvertFrom-Json
    foreach ($kind in @('encoder','decoder')) {
        $file = $modelManifest.$kind
        if ([IO.Path]::GetFileName($file) -ne $file -or -not $file.EndsWith('.sentis')) { throw 'Unsafe model filename' }
        $expected = $modelManifest.($kind + '_sha256')
        if ((Get-FileHash -LiteralPath (Join-Path $modelRoot $file) -Algorithm SHA256).Hash -ne $expected) { throw "Model hash mismatch: $file" }
    }
    $modelArgs = @(); if ($Serial) { $modelArgs = @('-s', $Serial) }
    & $Adb @modelArgs get-state
    if ($LASTEXITCODE -ne 0) { throw 'Connect one authorized headset, or pass -Serial.' }
    $modelDestination = "/sdcard/Android/data/$ApplicationId/files/models"
    & $Adb @modelArgs shell mkdir -p $modelDestination
    if ($LASTEXITCODE -ne 0) { throw 'Install and launch the app first.' }
    foreach ($kind in @('encoder','decoder')) {
        $file=$modelManifest.$kind
        & $Adb @modelArgs push (Join-Path $modelRoot $file) "$modelDestination/$file.pending"
        if ($LASTEXITCODE -ne 0) { throw "Model transfer failed: $file" }
        & $Adb @modelArgs shell mv "$modelDestination/$file.pending" "$modelDestination/$file"
        if ($LASTEXITCODE -ne 0) { throw "Model activation failed: $file" }
    }
    & $Adb @modelArgs push (Join-Path $modelRoot 'models.json') "$modelDestination/models.json.pending"
    if ($LASTEXITCODE -ne 0) { throw 'Model manifest transfer failed.' }
    & $Adb @modelArgs shell mv "$modelDestination/models.json.pending" "$modelDestination/models.json"
    if ($LASTEXITCODE -ne 0) { throw 'Model manifest activation failed.' }
    Write-Host 'Pushed verified external SAM models. Restart TwinInspector to enable Pick object.'
    return
}
$source = Join-Path $root "work\twin\$Space"
$manifestPath = Join-Path $source 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Manifest missing: $manifestPath" }
if ($ApplicationId -notmatch '^[a-zA-Z0-9_.]+$') { throw 'Invalid application id' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$adbArguments = @()
if ($Serial) { $adbArguments = @('-s', $Serial) }
& $Adb @adbArguments get-state
if ($LASTEXITCODE -ne 0) { throw 'Connect one authorized headset, or pass -Serial.' }
$destination = "/sdcard/Android/data/$ApplicationId/files/twin/$Space"
$revision = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant()
$versionDestination = "$destination/versions/$revision"
& $Adb @adbArguments shell mkdir -p $versionDestination
if ($LASTEXITCODE -ne 0) { throw 'Cannot create device data directory. Install and launch the app first.' }
# Push only runtime files, never multi-gigabyte processing intermediates.
$tileRoot = Join-Path $source 'tiles'
# Galaxy XR allows shell mkdir here but rejects ADB sync's recursive secure_mkdirs.
# Create the tile directories explicitly before asking sync to copy their files.
$remoteTileDirectories = @("$versionDestination/tiles")
foreach ($directory in Get-ChildItem -LiteralPath $tileRoot -Directory -Recurse) {
    $relative = [IO.Path]::GetRelativePath($tileRoot, $directory.FullName).Replace('\', '/')
    if ($relative -notmatch '^[a-zA-Z0-9_/-]+$') { throw "Unsafe tile directory: $relative" }
    $remoteTileDirectories += "$versionDestination/tiles/$relative"
}
& $Adb @adbArguments shell mkdir -p @remoteTileDirectories
if ($LASTEXITCODE -ne 0) { throw 'Cannot create tile directories; previous manifest retained.' }
& $Adb @adbArguments push --sync $tileRoot "$versionDestination/"
if ($LASTEXITCODE -ne 0) { throw 'Tile transfer failed; previous manifest retained.' }
$markers = Join-Path $source 'markers.json'
if (Test-Path -LiteralPath $markers) {
    & $Adb @adbArguments push $markers "$destination/markers.json"
    if ($LASTEXITCODE -ne 0) { throw 'Marker transfer failed.' }
}
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
Write-Host "Pushed $Space ($($manifest.tiles.Count) tiles). Restart TwinInspector to load it."
