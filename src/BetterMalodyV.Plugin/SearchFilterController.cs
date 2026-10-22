using Malody.Scene.Panel;
using UnityEngine;

namespace BetterMalodyV.Plugin;

/// <summary>Find the active panel even if IL2CPP inlined Show or a popup uses another native path.</summary>
public sealed class SearchFilterController : MonoBehaviour
{
    public SearchFilterController(IntPtr pointer) : base(pointer) { }
    private float nextScan;
    private readonly HashSet<int> failures = new();
    public void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime+.2f;
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
