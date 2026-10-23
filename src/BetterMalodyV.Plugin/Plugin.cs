using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using BetterMalodyV.Shared;
using Malody.Scene.Panel;

namespace BetterMalodyV.Plugin;

[BepInPlugin("com.mcpsde.bettermalodyv", "BetterMalodyV", "1.3.0")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Current = null!;
    internal static GameAdapter Adapter = null!;
    private static ConfigEntry<int> selected = null!;
    private static bool failed;
    internal static object? CurrentSong;
    internal static StatusMask Selection => failed ? StatusMask.All : (StatusMask)selected.Value;

    public override void Load()
    {
        Current = this;
        selected = Config.Bind("Search", "StatusMask", 7, new ConfigDescription("Alpha=1, Beta=2, Stable=4. 7=all including unknown; 0=none.", new AcceptableValueRange<int>(0,7)));
        var harmony = new Harmony("com.mcpsde.bettermalodyv");
        try
        {
            Adapter = new GameAdapter();
            harmony.Patch(Adapter.Contains, postfix: Hook(nameof(FilterSong)));
            harmony.Patch(Adapter.Fill, prefix: Hook(nameof(RememberSong)));
            harmony.Patch(Adapter.RoomOpen, prefix: Hook(nameof(RoomBefore)), postfix: Hook(nameof(DisplayAfter)), finalizer: Hook(nameof(DisplayFinally)));
            harmony.Patch(Adapter.RefreshMoveNext, prefix: Hook(nameof(ChartBefore)), postfix: Hook(nameof(DisplayAfter)), finalizer: Hook(nameof(DisplayFinally)));
            harmony.Patch(typeof(PanelSearchSong).GetMethod("Show"), postfix: Hook(nameof(SearchShown)));
            AddComponent<SearchFilterController>();
            Adapter.StatusStore.RequestRefresh(Paths.GameRootPath);
            try { AddComponent<RoomChartController>().Initialize(harmony); }
            catch (Exception ex) { Log.LogError("Room chart updates unavailable: "+ex); }
            try {AddComponent<ReplayRunController>().Initialize(harmony);}
            catch(Exception ex){Log.LogError("Replay reenactment unavailable: "+ex);}
#if !BETTER_MALODYV_VERIFY
            AddComponent<GameUpdateController>();
#endif
            Log.LogInfo($"BetterMalodyV loaded. Local status filter: {Selection}. Contracts: {Adapter.SongType.FullName}, {Adapter.ManagerType.FullName}.");
#if BETTER_MALODYV_VERIFY
            RuntimeVerification.Install(this, harmony);
#endif
        }
        catch (Exception ex)
        {
            harmony.UnpatchSelf(); Disable(ex);
        }
    }

    private static HarmonyMethod Hook(string name) => new(typeof(Plugin).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!);
    private static void FilterSong(object __instance, object __0, ref bool __result)
    {
        if (!__result || Selection == StatusMask.All) return;
        try { __result = Adapter.SongMatches(__instance, __0); }
        catch (Exception ex) { Disable(ex); }
    }
    private static void RememberSong(object __0) => CurrentSong = __0;
    private static void RoomBefore(object __0, out GameAdapter.DisplayScope? __state) => Begin(__0, out __state);
    private static void ChartBefore(out GameAdapter.DisplayScope? __state) => Begin(CurrentSong, out __state);
    private static void Begin(object? song, out GameAdapter.DisplayScope? state)
    {
        state = null;
        try { state = Adapter.FilterDisplay(song); }
        catch (Exception ex) { Disable(ex); }
    }
    private static void DisplayAfter(GameAdapter.DisplayScope? __state) => __state?.Dispose();
    private static Exception? DisplayFinally(Exception? __exception, GameAdapter.DisplayScope? __state)
    { __state?.Dispose(); return __exception; }
    private static void SearchShown(PanelSearchSong __instance)
    {
        if (failed) return;
        try { SearchFilterUi.Attach(__instance); }
        catch (Exception ex) { Current.Log.LogError("Cannot add status controls: " + ex); }
    }
    internal static void SetSelection(StatusMask mask) { selected.Value = (int)mask; Current.Config.Save(); }
    internal void LogUiAttached(PanelSearchSong panel,float controlsWidth,float textWidth) =>
        Log.LogInfo($"Status controls attached inside search input: {panel.gameObject.name}, controls={controlsWidth:F0}, text viewport={textWidth:F0}.");
    internal void LogUiFailure(Exception ex) => Log.LogError("Cannot attach search status controls: "+ex);
    internal void LogIndexReady(int records,long milliseconds) => Log.LogInfo($"Local status index ready: {records} cached chart records, {milliseconds} ms on background worker.");
    internal void LogIndexFailure(Exception ex) => Log.LogWarning("Local status index unavailable; retaining previous snapshot: "+ex.Message);
    internal void LogRoom(string message) => Log.LogInfo(message);
    private static void Disable(Exception ex)
    {
        if (!failed) Current.Log.LogError("Status filtering disabled; original selection preserved. " + ex);
        failed = true;
    }
}
