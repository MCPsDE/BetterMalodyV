param([string]$GameRoot = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV', [switch]$ReuseHost,
    [int]$Width = 1440,[int]$Height = 900,[switch]$UseInstalledMetadata,[switch]$RoomVisual,[switch]$UpdateVisual,[switch]$ReplayVisual)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$hostRoot = Join-Path $repo 'artifacts\native-test'
if ((Test-Path $hostRoot) -and !$ReuseHost) { throw 'Native test host already exists; use -ReuseHost to retain and reuse it.' }
if (Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq (Join-Path $hostRoot 'Malody V.exe') }) { throw 'The isolated test host is already running.' }
if (!$ReuseHost) {
New-Item -ItemType Directory -Force $hostRoot | Out-Null
foreach ($name in @('Malody V.exe','GameAssembly.dll','UnityPlayer.dll','baselib.dll')) { Copy-Item (Join-Path $GameRoot $name) $hostRoot }
New-Item -ItemType Junction -Path (Join-Path $hostRoot 'Malody V_Data') -Target (Join-Path $GameRoot 'Malody V_Data') | Out-Null
Copy-Item (Join-Path $repo 'vendor\bepinex\*') $hostRoot -Recurse
foreach ($name in @('interop','unity-libs')) { Copy-Item (Join-Path $GameRoot "BepInEx\$name") (Join-Path $hostRoot 'BepInEx') -Recurse }
}
# Standard Steamworks development marker keeps the isolated host from being
# forwarded by Steam to the user's installed executable.
[IO.File]::WriteAllText((Join-Path $hostRoot 'steam_appid.txt'),'1512940')
if ($UseInstalledMetadata) {
    foreach ($name in @('chart.bin','song.bin')) { Copy-Item -LiteralPath (Join-Path $GameRoot $name) -Destination (Join-Path $hostRoot $name) -Force }
}
dotnet build (Join-Path $repo 'src\BetterMalodyV.Plugin') -c Release -p:RuntimeVerification=true "-p:GameInteropRoot=$GameRoot\BepInEx\interop" --artifacts-path (Join-Path $repo 'artifacts\native-test-build')
if ($LASTEXITCODE -ne 0) { throw 'Native verification build failed' }
$pluginRoot = Join-Path $hostRoot 'BepInEx\plugins\BetterMalodyV'
New-Item -ItemType Directory -Force $pluginRoot | Out-Null
Copy-Item (Join-Path $repo 'artifacts\native-test-build\bin\BetterMalodyV.Plugin\release\BetterMalodyV.*.dll') $pluginRoot
$env:BETTER_MALODYV_TEST_OUTPUT = $hostRoot
$env:BETTER_MALODYV_TEST_WIDTH = $Width.ToString()
$env:BETTER_MALODYV_TEST_HEIGHT = $Height.ToString()
$env:BETTER_MALODYV_TEST_METADATA = if ($UseInstalledMetadata) { '1' } else { '0' }
$env:BETTER_MALODYV_TEST_ROOM_VISUAL = if ($RoomVisual) {'1'}else{'0'}
$env:BETTER_MALODYV_TEST_UPDATE_VISUAL = if ($UpdateVisual) {'1'}else{'0'}
$env:BETTER_MALODYV_TEST_REPLAY_VISUAL = if ($ReplayVisual) {'1'}else{'0'}
try {
    foreach ($name in @('result.json','search-filter-fixture.png','replay-action.png','replay-analysis-overview.png','replay-analysis-tracks.png','replay-analysis-presses.png','replay-analysis-profiles.png')) {
        $previous = Join-Path $hostRoot $name
        if (Test-Path -LiteralPath $previous) { Remove-Item -LiteralPath $previous -Force }
    }
    $process = Start-Process -FilePath (Join-Path $hostRoot 'Malody V.exe') -WorkingDirectory $hostRoot -WindowStyle Hidden -ArgumentList @('-batchmode','-screen-width',$Width,'-screen-height',$Height,'-logFile',('"'+(Join-Path $hostRoot 'unity.log')+'"')) -PassThru
    $process.Id | Set-Content (Join-Path $hostRoot 'test-pid.txt')
    Write-Output "Isolated test started: PID $($process.Id), $hostRoot"
    if (!$process.WaitForExit(60000)) {
        $owned = Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)"
        if ($owned.ExecutablePath -eq (Join-Path $hostRoot 'Malody V.exe')) { Stop-Process -Id $process.Id -Force }
        throw 'Isolated runtime test did not complete within 60 seconds. Its owned test process was stopped.'
    }
    $result = Get-Content -LiteralPath (Join-Path $hostRoot 'result.json') -Raw | ConvertFrom-Json
    if (!$result.passed) { throw $result.error }
    $evidence = Join-Path $repo '.impeccable\review'
    Copy-Item (Join-Path $hostRoot 'search-filter-fixture.png') (Join-Path $evidence "search-native-$($Width)x$Height.png") -Force
    Copy-Item (Join-Path $hostRoot 'result.json') (Join-Path $evidence "search-native-$($Width)x$Height.json") -Force
    if($RoomVisual){Copy-Item (Join-Path $hostRoot 'room-update-fixture.png') (Join-Path $evidence "room-native-$($Width)x$Height.png") -Force}
    if($UpdateVisual){foreach($state in @('download','verifying','ready','failed','unknown')){Copy-Item (Join-Path $hostRoot "game-update-$state.png") (Join-Path $evidence "update-$state-$($Width)x$Height.png") -Force}}
    if($ReplayVisual){
        Copy-Item (Join-Path $hostRoot 'replay-action.png') (Join-Path $evidence "replay-action-$($Width)x$Height.png") -Force
        foreach($page in @('overview','tracks','presses','profiles')){
            Copy-Item (Join-Path $hostRoot "replay-analysis-$page.png") (Join-Path $evidence "replay-analysis-$page-$($Width)x$Height.png") -Force
        }
    }
    Write-Output "PASS: $($result.checks.Count) native checks using the real search prefab at $($Width)x$Height."
} finally { Remove-Item Env:BETTER_MALODYV_TEST_OUTPUT,Env:BETTER_MALODYV_TEST_WIDTH,Env:BETTER_MALODYV_TEST_HEIGHT,Env:BETTER_MALODYV_TEST_METADATA,Env:BETTER_MALODYV_TEST_ROOM_VISUAL,Env:BETTER_MALODYV_TEST_UPDATE_VISUAL,Env:BETTER_MALODYV_TEST_REPLAY_VISUAL }
