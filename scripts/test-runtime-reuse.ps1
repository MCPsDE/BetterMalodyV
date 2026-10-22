param([string]$InstallerDll = '', [string]$PayloadZip = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$InstallerDll) { $InstallerDll = Join-Path $repo 'artifacts\installer-build\bin\BetterMalodyV.Installer\release_win-x64\BetterMalodyV.Installer.dll' }
if (!$PayloadZip) { $PayloadZip = Join-Path $repo 'artifacts\payload.zip' }
$testRoot = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts\runtime-reuse-test'))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd('\') + '\'
if (!$testRoot.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test root.' }
if (Test-Path $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
$game = Join-Path $testRoot 'MalodyV'
New-Item -ItemType Directory -Path (Join-Path $game 'Malody V_Data\il2cpp_data') -Force | Out-Null
foreach($name in @('Malody V.exe','UnityPlayer.dll','GameAssembly.dll')) { [IO.File]::WriteAllBytes((Join-Path $game $name),[byte[]](1,2,3)) }
function Hash-Tree {
    @(Get-ChildItem -LiteralPath $game -Recurse -File | Sort-Object FullName | ForEach-Object {
        [IO.Path]::GetRelativePath($game,$_.FullName) + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    })
}
function Invoke-Installer([string]$command) {
    & dotnet $InstallerDll $command $game | Out-Null
    return $LASTEXITCODE
}
$env:BETTER_MALODYV_PAYLOAD = $PayloadZip
$env:BETTER_MALODYV_QUIET = '1'
$env:BETTER_MALODYV_DESKTOP = Join-Path $testRoot 'Desktop'
New-Item -ItemType Directory -Path $env:BETTER_MALODYV_DESKTOP -Force | Out-Null
try {
    $before = Hash-Tree
    if ((Invoke-Installer '--check') -ne 0) { throw 'Fresh dry-run failed.' }
    if (Compare-Object $before (Hash-Tree)) { throw 'Dry-run changed the game tree.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($PayloadZip)
    try {
        foreach($entry in $archive.Entries) {
            $relative = $entry.FullName.Replace('\','/')
            if ($relative.StartsWith('BetterMalodyV/') -or $relative.StartsWith('BepInEx/plugins/') -or !$entry.Name) { continue }
            $target = Join-Path $game $relative
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$true)
        }
    } finally { $archive.Dispose() }
    $plugin = Join-Path $game 'BepInEx\plugins\OtherPlugin.dll'
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($plugin)) -Force | Out-Null
    [IO.File]::WriteAllBytes($plugin,[byte[]](9,8,7))
    $config = Join-Path $game 'doorstop_config.ini'
    [IO.File]::AppendAllText($config,"`n# preserve custom runtime config`n")
    $external = Hash-Tree
    if ((Invoke-Installer '--check') -ne 0) { throw 'Compatible runtime dry-run failed.' }
    if (Compare-Object $external (Hash-Tree)) { throw 'Compatible dry-run changed files.' }
    if ((Invoke-Installer '--install') -ne 0) { throw 'Installing into existing BepInEx failed.' }
    $manifestPath = Join-Path $game 'BetterMalodyV\install-manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if (!$manifest.reusedBepInEx) { throw 'Existing runtime was not marked reused.' }
    foreach($file in $manifest.files) {
        if (!$file.relativePath.StartsWith('BetterMalodyV/') -and !$file.relativePath.StartsWith('BepInEx/plugins/BetterMalodyV/')) { throw "Took ownership of external runtime: $($file.relativePath)" }
    }
    $installedPlugin = Join-Path $game 'BepInEx\plugins\BetterMalodyV\BetterMalodyV.Plugin.dll'
    Remove-Item -LiteralPath $installedPlugin -Force
    if ((Invoke-Installer '--install') -ne 0 -or !(Test-Path $installedPlugin)) { throw 'Repair on reused runtime failed.' }
    if ((Invoke-Installer '--verify') -ne 0) { throw 'Verify on reused runtime failed.' }
    # Force a mid-install write failure after earlier files have been replaced.
    Remove-Item -LiteralPath $installedPlugin -Force
    $beforeFailure = Hash-Tree
    $lockedPath = Join-Path $game 'BetterMalodyV\BetterMalodyV.Manager.exe'
    $locked = [IO.File]::Open($lockedPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
    try {
        if ((Invoke-Installer '--install') -eq 0) { throw 'Locked destination did not fail.' }
    } finally { $locked.Dispose() }
    if (Compare-Object $beforeFailure (Hash-Tree)) { throw 'Rollback did not restore the exact pre-install file tree.' }
    if ((Invoke-Installer '--install') -ne 0) { throw 'Repair after rollback failed.' }
    if ((Invoke-Installer '--uninstall') -ne 0) { throw 'Uninstall with reused runtime failed.' }
    if (Compare-Object $external (Hash-Tree)) { throw 'Reused BepInEx or unrelated plugin changed.' }

    $originalConfig = [IO.File]::ReadAllText($config)
    foreach($invalidConfig in @($originalConfig.Replace('enabled = true','enabled = false'),$originalConfig.Replace('BepInEx.Unity.IL2CPP.dll','BepInEx.Unity.Mono.dll'))) {
        [IO.File]::WriteAllText($config,$invalidConfig)
        $beforeInvalid = Hash-Tree
        if ((Invoke-Installer '--install') -eq 0) { throw 'Incompatible runtime was accepted.' }
        if (Compare-Object $beforeInvalid (Hash-Tree)) { throw 'Rejected environment was modified.' }
    }
    [IO.File]::WriteAllText($config,$originalConfig)
    $harmony = Join-Path $game 'BepInEx\core\0Harmony.dll'
    [IO.File]::Move($harmony,$harmony+'.test-backup')
    $beforeMissing = Hash-Tree
    if ((Invoke-Installer '--install') -eq 0) { throw 'Incomplete runtime was accepted.' }
    if (Compare-Object $beforeMissing (Hash-Tree)) { throw 'Incomplete runtime was modified.' }
    [IO.File]::Move($harmony+'.test-backup',$harmony)
    Write-Output 'Runtime reuse, ownership, dry-run, rollback and incompatible-environment tests passed.'
} finally {
    Remove-Item Env:BETTER_MALODYV_PAYLOAD,Env:BETTER_MALODYV_QUIET,Env:BETTER_MALODYV_DESKTOP -ErrorAction SilentlyContinue
}
