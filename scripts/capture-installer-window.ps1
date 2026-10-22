$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Add-Type -AssemblyName System.Windows.Forms
[Windows.Forms.Application]::SetHighDpiMode([Windows.Forms.HighDpiMode]::PerMonitorV2) | Out-Null
[Windows.Forms.Application]::EnableVisualStyles()
$dll = Join-Path $repo 'src\BetterMalodyV.Installer\bin\Release\net8.0-windows\win-x64\BetterMalodyV.Installer.dll'
$assembly = [Reflection.Assembly]::LoadFrom($dll)
$form = [Activator]::CreateInstance($assembly.GetType('BetterMalodyV.Installer.InstallerForm'),[Reflection.BindingFlags]'Instance,Public,NonPublic',$null,[object[]]@(),$null)
try {
    $form.Size = $form.MinimumSize
    $form.ShowInTaskbar = $false
    $form.TopMost = $true
    $form.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $form.Location = [Drawing.Point]::new(30,30)
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 350
    $helper = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex\skills\screenshot\scripts\take_screenshot.ps1'
    $temporaryImage = & $helper -Mode temp -WindowHandle ([int]$form.Handle)
    $destination = Join-Path $repo '.impeccable\review\installer-minimum-native.png'
    Copy-Item -LiteralPath $temporaryImage -Destination $destination -Force
    Write-Output $destination
} finally { $form.Close(); $form.Dispose() }
