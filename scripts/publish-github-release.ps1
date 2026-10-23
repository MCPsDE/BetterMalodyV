param([switch]$SkipBuild, [switch]$Draft)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = (Select-Xml -Path (Join-Path $repoRoot 'Directory.Build.props') -XPath '/Project/PropertyGroup/Version').Node.InnerText
if (!$SkipBuild) {
    pwsh -NoProfile -File (Join-Path $PSScriptRoot 'build-release.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
$releaseRoot = Join-Path $repoRoot 'artifacts\release'
$assets = @((Join-Path $releaseRoot "BetterMalodyV-Setup-$version.exe"),(Join-Path $releaseRoot 'SHA256SUMS.txt'),(Join-Path $releaseRoot 'README.txt'))
foreach ($asset in $assets) { if (!(Test-Path $asset -PathType Leaf)) { throw "Missing release asset: $asset" } }
$head = git -C $repoRoot rev-parse HEAD
$remoteHead = gh api repos/MCPsDE/BetterMalodyV/commits/main --jq .sha
if ($LASTEXITCODE -ne 0 -or $head -ne $remoteHead) { throw 'Push the release source commit to main before publishing.' }
$notes = Join-Path $repoRoot 'docs\FEATURES.md'
$arguments = @('release','create',"v$version") + $assets + @('--repo','MCPsDE/BetterMalodyV','--target',$head,'--title',"BetterMalodyV $version",'--notes-file',$notes)
if ($Draft) { $arguments += '--draft' }
& gh @arguments
if ($LASTEXITCODE -ne 0) { throw 'GitHub release failed' }
