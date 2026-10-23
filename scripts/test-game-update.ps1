param([string]$InstallerExe='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version=(Select-Xml -Path (Join-Path $repo 'Directory.Build.props') -XPath '/Project/PropertyGroup/Version').Node.InnerText
if(!$InstallerExe){$InstallerExe=Join-Path $repo "artifacts\release\BetterMalodyV-Setup-$version.exe"}
$fixture=Join-Path $repo ('artifacts\game-update-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force (Join-Path $fixture 'Malody V_Data\il2cpp_data') | Out-Null
dotnet publish (Join-Path $repo 'tests\RestartGameFixture') -c Release -o $fixture -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Restart fixture build failed'}
foreach($name in @('UnityPlayer.dll','GameAssembly.dll')){[IO.File]::WriteAllBytes((Join-Path $fixture $name),[byte[]](1,2,3))}
$env:BETTER_MALODYV_DESKTOP=Join-Path $fixture 'Desktop'
try{
    $game=Start-Process -FilePath (Join-Path $fixture 'Malody V.exe') -WorkingDirectory $fixture -PassThru -WindowStyle Hidden
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    while(!(Test-Path (Join-Path $fixture 'fixture-pid.txt')) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 200}
    $process=Start-Process -FilePath $InstallerExe -ArgumentList @('--update-game',('"'+$fixture+'"'),'2147483647',$game.Id) -PassThru -WindowStyle Hidden
    $request=Join-Path $fixture "BetterMalodyV\.updates\quit-$($game.Id).request"
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    while(!(Test-Path $request) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
    if(!(Test-Path $request)){throw 'Installer did not issue graceful quit request'}
    $duplicate=Start-Process -FilePath $InstallerExe -ArgumentList @('--update-game',('"'+$fixture+'"'),'2147483647',$game.Id) -PassThru -WindowStyle Hidden
    if(!$duplicate.WaitForExit(10000) -or $duplicate.ExitCode -ne 0){throw 'Duplicate update handoff did not exit through single-owner gate'}
    if(!$process.WaitForExit(60000)){throw 'Restart protocol timeout'}
    if($process.ExitCode -ne 0){throw 'Restart installer failed'}
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    while([DateTime]::UtcNow -lt $deadline){if((Get-Content (Join-Path $fixture 'fixture-starts.txt') -Raw).Trim() -eq '2'){break};Start-Sleep -Milliseconds 200}
    if(!(Test-Path (Join-Path $fixture 'fixture-graceful-exit.txt'))){throw 'Game did not receive graceful quit request'}
    if((Get-Content (Join-Path $fixture 'fixture-starts.txt') -Raw).Trim() -ne '2'){throw 'Game was not restarted exactly once'}
    $manifest=Get-Content (Join-Path $fixture 'BetterMalodyV\install-manifest.json') -Raw | ConvertFrom-Json
    if($manifest.productVersion -ne $version){throw 'Restart updated wrong version'}
    Write-Output "PASS: graceful game exit, $version install, duplicate handoff rejected, automatic restart exactly once. Isolated executable fixture: $fixture"
}finally{Remove-Item Env:BETTER_MALODYV_DESKTOP}
