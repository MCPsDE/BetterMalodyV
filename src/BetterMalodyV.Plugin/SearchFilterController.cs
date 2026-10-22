using Malody.Scene.Panel;
using UnityEngine;

namespace BetterMalodyV.Plugin;

/// <summary>Find the active panel even if IL2CPP inlined Show or a popup uses another native path.</summary>
public sealed class SearchFilterController : MonoBehaviour
{
    public SearchFilterController(IntPtr pointer) : base(pointer) { }
    private float nextScan;
    private float nextIndex;
    private readonly HashSet<int> failures = new();
    public void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime+.2f;
        var store = Plugin.Adapter.StatusStore;
        if (Time.unscaledTime >= nextIndex)
        {
            nextIndex = Time.unscaledTime+5;
            store.RequestRefresh(BepInEx.Paths.GameRootPath);
        }
        if (store.RefreshPending)
        {
            var generation = store.Generation;
            try
            {
                if (Plugin.Selection == Shared.StatusMask.All || Plugin.Adapter.RefreshAfterIndex()) store.MarkApplied(generation);
            }
            catch (Exception ex)
            {
                store.MarkApplied(generation); Plugin.Current.LogUiFailure(ex);
            }
        }
        Scan();
    }
    internal void Scan()
    {
        foreach (var panel in UnityEngine.Object.FindObjectsOfType<PanelSearchSong>())
        {
            if (!panel || !panel.gameObject.activeInHierarchy || failures.Contains(panel.GetInstanceID())) continue;
            try { SearchFilterUi.Attach(panel); }
            catch (Exception ex)
            {
                failures.Add(panel.GetInstanceID());
                Plugin.Current.LogUiFailure(ex);
            }
        }
    }
}
