using System.Reflection;
using Malody.DB;
using Malody.Chart;
using Malody.Scene;
using Malody.Scene.MP;

namespace BetterMalodyV.Plugin;

/// <summary>Bind unique public contracts, never obfuscated class/field aliases.</summary>
internal sealed class GameAdapter
{
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
    public Type SongType { get; }
    public Type ManagerType { get; }
    public MethodInfo Contains { get; }
    public MethodInfo RefreshMoveNext { get; }
    public MethodInfo RoomOpen { get; }
    public MethodInfo Fill { get; }
    private readonly MethodInfo scan;
    private readonly PropertyInfo showList, chartMeta, chartMode, filterMode;
    private readonly MethodInfo count, item, clear, add;

    public GameAdapter()
    {
        var framework = typeof(PlayModeType).Assembly;
        SongType = framework.GetTypes().Single(t => t.Namespace == "Malody.Chart" && t.GetMethod("RebuildShowList", Type.EmptyTypes) is not null);
        ManagerType = framework.GetTypes().Single(t => t.Namespace == "Malody.Chart" && t.GetMethod("RefreshDisplayList", new[] { typeof(bool) }) is not null);
        Contains = ManagerType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(t => t.GetMethods(Instance | BindingFlags.DeclaredOnly))
            .Single(m => m.Name == "IsContain" && m.ReturnType == typeof(bool) && m.GetParameters() is var p && p.Length == 1 && p[0].ParameterType == SongType);
        filterMode = Contains.DeclaringType!.GetProperties(Instance).Single(p => p.PropertyType == typeof(PlayModeType));
        scan = SongType.GetMethod("ScanFiles", new[] { typeof(bool) })!;
        showList = SongType.GetProperties(Instance).Single(p => !p.CanWrite && p.PropertyType.IsGenericType
            && p.PropertyType.GetGenericTypeDefinition().FullName == "Il2CppSystem.Collections.Generic.List`1");
        var chartType = showList.PropertyType.GenericTypeArguments[0];
        chartMeta = chartType.GetProperties(Instance).Single(p => p.PropertyType == typeof(ChartRecord));
        chartMode = chartType.GetProperties(Instance).Single(p => p.PropertyType == typeof(PlayModeType));
        var list = showList.PropertyType;
        count = list.GetProperty("Count")!.GetMethod!; item = list.GetProperty("Item")!.GetMethod!;
        clear = list.GetMethod("Clear", Type.EmptyTypes)!; add = list.GetMethod("Add", new[] { chartType })!;
        Fill = typeof(SceneChart).GetMethod("Fill", new[] { SongType })!;
        RoomOpen = typeof(PanelRoomChart).GetMethod("OpenChart", new[] { SongType })!;
        RefreshMoveNext = typeof(SceneChart).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Single(t => t.Name.StartsWith("_Refresh_d__", StringComparison.Ordinal))
            .GetMethod("MoveNext", Instance | BindingFlags.NonPublic)!;
        if (new[] { scan, Fill, RoomOpen, RefreshMoveNext }.Any(m => m is null))
            throw new NotSupportedException("The game's song selection contracts changed.");
    }

    public bool SongMatches(object filter, object song)
    {
        var mask = Plugin.Selection;
        if (mask == Shared.StatusMask.All) return true;
        if (mask == Shared.StatusMask.None) return false;
        // Use the same lazy local database-backed loading as the game's chart page.
        // This also avoids relying on SongRecord.Searchwords, which can be stale
        // after the game updates an individual ChartRecord.RankStatus.
        scan.Invoke(song, new object[] { false });
        var mode = (PlayModeType)filterMode.GetValue(filter)!;
        foreach (var chart in Snapshot(showList.GetValue(song)!))
            if ((mode == PlayModeType.Max || (PlayModeType)chartMode.GetValue(chart)! == mode)
                && Shared.StatusSelection.Matches(mask, ((ChartRecord)chartMeta.GetValue(chart)!).RankStatus)) return true;
        return false;
    }

    public DisplayScope? FilterDisplay(object? song)
    {
        if (song is null || Plugin.Selection == Shared.StatusMask.All) return null;
        scan.Invoke(song, new object[] { false });
        var list = showList.GetValue(song)!;
        var original = Snapshot(list);
        var filtered = original.Where(c => Shared.StatusSelection.Matches(Plugin.Selection, ((ChartRecord)chartMeta.GetValue(c)!).RankStatus)).ToArray();
        var scope = new DisplayScope(this, list, original);
        try { Replace(list, filtered); return scope; }
        catch { scope.Dispose(); throw; }
    }

    private object[] Snapshot(object list)
    {
        var result = new object[(int)count.Invoke(list, null)!];
        for (var i = 0; i < result.Length; i++) result[i] = item.Invoke(list, new object[] { i })!;
        return result;
    }

    private void Replace(object list, object[] charts)
    {
        clear.Invoke(list, null);
        foreach (var chart in charts) add.Invoke(list, new[] { chart });
    }

    public sealed class DisplayScope : IDisposable
    {
        private readonly GameAdapter adapter;
        private object? list;
        private readonly object[] original;
        public DisplayScope(GameAdapter adapter, object list, object[] original) { this.adapter = adapter; this.list = list; this.original = original; }
        public void Dispose() { if (list is null) return; var target = list; list = null; adapter.Replace(target, original); }
    }
}
