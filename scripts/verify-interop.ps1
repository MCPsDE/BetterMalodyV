param([string]$InteropRoot = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV\BepInEx\interop', [string]$PluginPath = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$PluginPath) { $PluginPath = Join-Path $repo 'artifacts\plugin-build\bin\BetterMalodyV.Plugin\release\BetterMalodyV.Plugin.dll' }
Add-Type -Path (Join-Path $repo 'vendor\bepinex\BepInEx\core\Mono.Cecil.dll')
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
foreach ($path in @($InteropRoot,(Join-Path $repo 'vendor\bepinex\BepInEx\core'),[IO.Path]::GetDirectoryName($PluginPath))) { $resolver.AddSearchDirectory($path) }
$parameters = [Mono.Cecil.ReaderParameters]::new(); $parameters.AssemblyResolver = $resolver
$module = [Mono.Cecil.ModuleDefinition]::ReadModule($PluginPath,$parameters)
try {
    $checked = 0
    foreach ($reference in $module.GetMemberReferences()) {
        $scope = $reference.DeclaringType.GetElementType().Scope
        if ($scope -isnot [Mono.Cecil.AssemblyNameReference]) { continue }
        if ($scope.Name -notin @('Assembly-CSharp','Framework','Protocol','Unity.TextMeshPro') -and !$scope.Name.StartsWith('UnityEngine')) { continue }
        if (!$reference.Resolve()) { throw "Missing runtime API: $reference" }; $checked++
    }
    $framework = $resolver.Resolve([Mono.Cecil.AssemblyNameReference]::Parse('Framework')).MainModule
    $songs = @($framework.Types | Where-Object { @($_.Methods | Where-Object Name -eq 'RebuildShowList').Count -eq 1 })
    $managers = @($framework.Types | Where-Object { @($_.Methods | Where-Object Name -eq 'RefreshDisplayList').Count -eq 1 })
    if ($songs.Count -ne 1 -or $managers.Count -ne 1) { throw 'Song/manager shape is ambiguous' }
    $song = $songs[0]; $manager = $managers[0]
    foreach ($contract in @(@($song,'get_FullLoaded'),@($manager,'get_Scanning'))) {
        $methods = @($contract[0].Methods | Where-Object Name -eq $contract[1])
        if ($methods.Count -ne 1 -or $methods[0].ReturnType.FullName -ne 'System.Boolean' -or $methods[0].Parameters.Count -ne 0) { throw "Status index lifecycle accessor changed: $($contract[1])" }
    }
    $singletons = @($manager.BaseType.Resolve().Properties | Where-Object { $_.SetMethod -and $_.GetMethod.IsStatic })
    if ($singletons.Count -ne 1) { throw 'Manager instance field is ambiguous' }
    $show = @($song.Properties | Where-Object { !$_.SetMethod -and $_.PropertyType.FullName.StartsWith('Il2CppSystem.Collections.Generic.List`1') })
    if ($show.Count -ne 1) { throw 'ShowList contract changed' }
    $chartType = $show[0].PropertyType.GenericArguments[0].Resolve()
    $meta = @($chartType.Properties | Where-Object { $_.PropertyType.FullName -eq 'Malody.DB.ChartRecord' })
    if ($meta.Count -ne 1) { throw 'ChartRecord contract changed' }
    $filters = @($manager.NestedTypes | Where-Object { @($_.Methods | Where-Object { $_.Name -eq 'IsContain' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq $song.FullName }).Count -eq 1 })
    if ($filters.Count -ne 1) { throw 'IsContain contract changed' }
    $assembly = $resolver.Resolve([Mono.Cecil.AssemblyNameReference]::Parse('Assembly-CSharp')).MainModule
    $scene = $assembly.GetType('Malody.Scene.SceneChart')
    $refresh = @($scene.NestedTypes | Where-Object { $_.Name.StartsWith('_Refresh_d__') })
    if ($refresh.Count -ne 1 -or @($refresh[0].Methods | Where-Object Name -eq 'MoveNext').Count -ne 1) { throw 'Refresh state machine changed' }
    foreach ($target in @(@('Malody.Scene.SceneChart','Fill'),@('Malody.Scene.MP.PanelRoomChart','OpenChart'),@('Malody.Scene.Panel.PanelSearchSong','Show'))) {
        $type = $assembly.GetType($target[0])
        if (@($type.Methods | Where-Object Name -eq $target[1]).Count -ne 1) { throw "UI target changed: $target" }
    }
    $share=$assembly.GetType('Malody.Scene.MP.WidgetMPShare')
    $receiver=@($share.NestedTypes|Where-Object {$_.Name.StartsWith('_HandleChartShare_d__')})
    if($receiver.Count -ne 1 -or @($receiver[0].Methods|Where-Object Name -eq 'MoveNext').Count -ne 1){throw 'Room chart-share receiver changed'}
    $online=@($framework.Types|Where-Object {@($_.Methods|Where-Object Name -eq 'DownloadAndImport').Count -eq 2})
    if($online.Count -ne 1){throw 'Official download API changed'}
    $download=@($online[0].Methods|Where-Object {$_.Name -eq 'DownloadAndImport' -and $_.Parameters.Count -eq 1})[0]
    $record=$download.Parameters[0].ParameterType.Resolve()
    foreach($field in @(@('yug','System.UInt32'),@('yuh','System.UInt32'),@('yuj','System.String'),@('yuk','System.String'),@('version','System.String'))){
        $properties=@($record.Properties|Where-Object Name -eq $field[0])
        if($properties.Count -ne 1 -or $properties[0].PropertyType.FullName -ne $field[1]){throw "Room download field changed: $($field[0])"}
    }
    Write-Output "PASS: $checked game/Unity references; unique Song, ChartRecord, IsContain and both selection UI contracts."
} finally { $module.Dispose(); $resolver.Dispose() }
