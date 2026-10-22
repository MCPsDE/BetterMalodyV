param([string]$InstallerExe = '')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version=(Select-Xml -Path (Join-Path $repo 'Directory.Build.props') -XPath '/Project/PropertyGroup/Version').Node.InnerText
if (!$InstallerExe) { $InstallerExe=Join-Path $repo "artifacts\release\BetterMalodyV-Setup-$version.exe" }
$InstallerExe=[IO.Path]::GetFullPath($InstallerExe)
$fixture=Join-Path $repo ('artifacts\package-test-'+$version+'-'+[Guid]::NewGuid().ToString('N'))
$game=Join-Path $fixture 'MalodyV'
New-Item -ItemType Directory -Force (Join-Path $game 'Malody V_Data\il2cpp_data') | Out-Null
foreach($name in @('Malody V.exe','UnityPlayer.dll','GameAssembly.dll')) { [IO.File]::WriteAllBytes((Join-Path $game $name),[byte[]](1,2,3)) }
$env:BETTER_MALODYV_DESKTOP=Join-Path $fixture 'Desktop'
$env:BETTER_MALODYV_QUIET='1'
try {
    foreach($command in @('--install','--verify')) {
        $process=Start-Process -FilePath $InstallerExe -ArgumentList @($command,('"'+$game+'"')) -PassThru -Wait -WindowStyle Hidden
        if($process.ExitCode -ne 0) { throw "Package command failed: $command" }
    }
    $manifest=Get-Content (Join-Path $game 'BetterMalodyV\install-manifest.json') -Raw | ConvertFrom-Json
    if($manifest.productVersion -ne $version) { throw 'Embedded installer version mismatch' }
    foreach($name in @('BetterMalodyV.Plugin.dll','BetterMalodyV.Shared.dll')) {
        $relative='BepInEx\plugins\BetterMalodyV\'+$name
        if((Get-FileHash (Join-Path $game $relative)).Hash -ne (Get-FileHash (Join-Path $repo ('artifacts\payload\'+$relative))).Hash) { throw "Embedded payload mismatch: $name" }
    }
    $process=Start-Process -FilePath $InstallerExe -ArgumentList @('--uninstall',('"'+$game+'"')) -PassThru -Wait -WindowStyle Hidden
    if($process.ExitCode -ne 0 -or (Test-Path (Join-Path $game 'winhttp.dll'))) { throw 'Package uninstall failed' }
    Write-Output "PASS: embedded $version install/verify/uninstall and plugin/shared hashes. Fixture: $fixture"
} finally { Remove-Item Env:BETTER_MALODYV_DESKTOP,Env:BETTER_MALODYV_QUIET }
