param([string]$GameRoot = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$download = Join-Path $repo 'artifacts\downloads\BepInEx-785.zip'
New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($download)) | Out-Null
$expected = '2a7cbf74d26abe4765c3e662db1721b923bac39849ebfef2ca5dc7de7e2d9b7f'
if (!(Test-Path $download)) {
    Invoke-WebRequest 'https://builds.bepinex.dev/projects/bepinex_be/785/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785%2B6abdba4.zip' -OutFile $download
}
if ((Get-FileHash $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'BepInEx SHA-256 mismatch' }
Expand-Archive $download (Join-Path $repo 'vendor\bepinex') -Force
$interop = Join-Path $GameRoot 'BepInEx\interop'
if (!(Test-Path (Join-Path $interop 'Assembly-CSharp.dll'))) { throw '需要所选游戏当前版本的 BepInEx interop 缓存。先安装 BepInEx 并启动游戏一次。' }
New-Item -ItemType Directory -Force (Join-Path $repo 'generated\game-libs') | Out-Null
Copy-Item (Join-Path $interop '*.dll') (Join-Path $repo 'generated\game-libs') -Force
Write-Output 'Pinned BepInEx verified; current game bindings copied for compilation.'
