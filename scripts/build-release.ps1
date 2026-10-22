param([string]$GameRoot = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifacts = Join-Path $repo 'artifacts'
$version = (Select-Xml -Path (Join-Path $repo 'Directory.Build.props') -XPath '/Project/PropertyGroup/Version').Node.InnerText
$payload = Join-Path $artifacts 'payload'
$release = Join-Path $artifacts 'release'
function Reset-Artifact([string]$path) {
    $resolved = [IO.Path]::GetFullPath($path)
    if (!$resolved.StartsWith($artifacts.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe output path' }
    if (Test-Path $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    New-Item -ItemType Directory -Path $resolved -Force | Out-Null
}
function Build-Project([string]$project, [switch]$Publish) {
    $path = Join-Path $repo "src\BetterMalodyV.$project"
    if ($Publish) { dotnet publish $path -c Release -r win-x64 --self-contained true --artifacts-path (Join-Path $artifacts "$($project.ToLower())-build") -p:UseSharedCompilation=false }
    else { dotnet build $path -c Release --artifacts-path (Join-Path $artifacts 'plugin-build') "-p:GameInteropRoot=$GameRoot\BepInEx\interop" -p:UseSharedCompilation=false }
    if ($LASTEXITCODE -ne 0) { throw "$project build failed" }
}
Reset-Artifact $payload
New-Item -ItemType Directory -Force -Path $release | Out-Null
Build-Project 'Plugin'
pwsh -NoProfile -File (Join-Path $PSScriptRoot 'verify-interop.ps1') -InteropRoot (Join-Path $GameRoot 'BepInEx\interop')
if ($LASTEXITCODE -ne 0) { throw 'Interop verification failed' }
Copy-Item (Join-Path $repo 'vendor\bepinex\*') $payload -Recurse -Force
$config = Join-Path $payload 'doorstop_config.ini'
[IO.File]::WriteAllText($config, [IO.File]::ReadAllText($config).Replace('ignore_disable_switch = false','ignore_disable_switch = true'))
Copy-Item (Join-Path $GameRoot 'BepInEx\interop') (Join-Path $payload 'BepInEx') -Recurse -Force
Copy-Item (Join-Path $GameRoot 'BepInEx\unity-libs') (Join-Path $payload 'BepInEx') -Recurse -Force
New-Item -ItemType Directory -Force (Join-Path $payload 'BepInEx\config') | Out-Null
Copy-Item (Join-Path $repo 'packaging\BepInEx.cfg') (Join-Path $payload 'BepInEx\config')
$pluginTarget = Join-Path $payload 'BepInEx\plugins\BetterMalodyV'
New-Item -ItemType Directory -Force $pluginTarget | Out-Null
foreach ($name in @('BetterMalodyV.Plugin.dll','BetterMalodyV.Shared.dll')) {
    Copy-Item (Join-Path $artifacts "plugin-build\bin\BetterMalodyV.Plugin\release\$name") $pluginTarget
}
Build-Project 'Manager' -Publish
$productTarget = Join-Path $payload 'BetterMalodyV'
New-Item -ItemType Directory -Force (Join-Path $productTarget 'docs') | Out-Null
Copy-Item (Join-Path $artifacts 'manager-build\publish\BetterMalodyV.Manager\release_win-x64\BetterMalodyV.Manager.exe') $productTarget
Copy-Item (Join-Path $repo 'docs\FEATURES.md') (Join-Path $productTarget 'docs')
Copy-Item (Join-Path $repo 'packaging\THIRD_PARTY_NOTICES.txt') (Join-Path $productTarget 'docs')
Copy-Item (Join-Path $repo 'packaging\licenses') (Join-Path $productTarget 'docs') -Recurse
$zip = Join-Path $artifacts 'payload.zip'
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
[IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
Build-Project 'Installer' -Publish
$exe = Join-Path $release "BetterMalodyV-Setup-$version.exe"
Copy-Item (Join-Path $artifacts 'installer-build\publish\BetterMalodyV.Installer\release_win-x64\BetterMalodyV.Installer.exe') $exe -Force
Copy-Item (Join-Path $repo 'README.md') (Join-Path $release 'README.txt') -Force
@($exe,(Join-Path $release 'README.txt')) | ForEach-Object {
    (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_)
} | Set-Content (Join-Path $release 'SHA256SUMS.txt') -Encoding utf8
Write-Output "Release ready: $exe"
