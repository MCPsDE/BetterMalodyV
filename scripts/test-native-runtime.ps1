param([string]$GameRoot = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV', [switch]$ReuseHost)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$hostRoot = Join-Path $repo 'artifacts\native-test'
if ((Test-Path $hostRoot) -and !$ReuseHost) { throw 'Native test host already exists; use -ReuseHost to retain and reuse it.' }
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
dotnet build (Join-Path $repo 'src\BetterMalodyV.Plugin') -c Release -p:RuntimeVerification=true "-p:GameInteropRoot=$GameRoot\BepInEx\interop" --artifacts-path (Join-Path $repo 'artifacts\native-test-build')
if ($LASTEXITCODE -ne 0) { throw 'Native verification build failed' }
$pluginRoot = Join-Path $hostRoot 'BepInEx\plugins\BetterMalodyV'
New-Item -ItemType Directory -Force $pluginRoot | Out-Null
Copy-Item (Join-Path $repo 'artifacts\native-test-build\bin\BetterMalodyV.Plugin\release\BetterMalodyV.*.dll') $pluginRoot
$env:BETTER_MALODYV_TEST_OUTPUT = $hostRoot
try {
    $process = Start-Process -FilePath (Join-Path $hostRoot 'Malody V.exe') -WorkingDirectory $hostRoot -WindowStyle Hidden -ArgumentList @('-batchmode','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $hostRoot 'unity.log')+'"')) -PassThru
    $process.Id | Set-Content (Join-Path $hostRoot 'test-pid.txt')
    Write-Output "Isolated test started: PID $($process.Id), $hostRoot"
} finally { Remove-Item Env:BETTER_MALODYV_TEST_OUTPUT }
