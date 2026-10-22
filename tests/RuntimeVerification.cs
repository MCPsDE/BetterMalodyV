using System.Reflection;
using System.Text.Json;
using BetterMalodyV.Shared;
using HarmonyLib;
using Malody.Chart;
using Malody.DB;
using Malody.Scene;
using Malody.Scene.Panel;
using Malody.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BetterMalodyV.Plugin;

// Compiled only for the isolated verification host; absent from shipping DLLs.
public sealed class RuntimeVerification : MonoBehaviour
{
    public RuntimeVerification(IntPtr pointer) : base(pointer) { }
    private bool ran;
    private float captureAt = float.PositiveInfinity;
    private float exitAt = float.PositiveInfinity;
    private static string Root => Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_OUTPUT")!;
    private readonly List<string> checks = new();
    private Camera? captureCamera;
    private PanelSearchSong? realPanel;
    private static int Width => int.TryParse(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_WIDTH"),out var width) ? width : 1440;
    private static int Height => int.TryParse(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_HEIGHT"),out var height) ? height : 900;
    public static void Install(Plugin plugin, Harmony harmony)
    {
        if (string.IsNullOrWhiteSpace(Root)) throw new InvalidOperationException("Verification output required");
        harmony.Patch(AccessTools.Method(typeof(SceneLogo),"Start"), prefix: new HarmonyMethod(typeof(RuntimeVerification),nameof(SkipLogo)));
        plugin.AddComponent<RuntimeVerification>();
    }
    private static bool SkipLogo() => false;
    public void Update()
    {
        if (ran)
        {
            if (Time.unscaledTime >= captureAt && captureCamera is not null)
            {
                captureAt = float.PositiveInfinity;
                try { ValidateRealPanel(); }
                catch (Exception ex) { WriteResult(ex); }
                Canvas.ForceUpdateCanvases(); captureCamera.Render();
                RenderTexture.active = captureCamera.targetTexture;
                var image = new Texture2D(Width,Height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,Width,Height),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(Root,"search-filter-fixture.png"),ImageConversion.EncodeToPNG(image));
                RenderTexture.active = null;
                exitAt = Time.unscaledTime+.2f;
            }
            if (Time.unscaledTime >= exitAt) Application.Quit();
            return;
        }
        ran = true;
        try
        {
            TestNativeData(); TestControls();
            captureAt = Time.unscaledTime+1.2f;
        }
        catch (Exception ex)
        {
            WriteResult(ex);
            exitAt = Time.unscaledTime+.2f;
        }
    }
    private void Check(bool condition,string label) { if (!condition) throw new Exception(label); checks.Add(label); }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void WriteResult(Exception? error = null) => File.WriteAllText(Path.Combine(Root,"result.json"),
        JsonSerializer.Serialize(new { passed = error is null,error = error?.ToString(),checks },new JsonSerializerOptions { WriteIndented = true }));
    private void TestNativeData()
    {
        var adapter = Plugin.Adapter;
        var filter = Activator.CreateInstance(adapter.Contains.DeclaringType!)!;
        var mode = filter.GetType().GetProperties().Single(p => p.PropertyType == typeof(PlayModeType));
        var song = Activator.CreateInstance(adapter.SongType)!;
        var metaProperty = adapter.SongType.GetProperties().Single(p => p.PropertyType == typeof(SongRecord));
        var songMeta = (SongRecord)metaProperty.GetValue(song)!;
        songMeta.Title = "Synthetic fixture";
        adapter.SongType.GetProperties().Single(p => p.PropertyType == typeof(bool) && p.Name.StartsWith("_") && p.Name.Contains("BackingField")).SetValue(song,true);
        var show = adapter.SongType.GetProperties().Single(p => !p.CanWrite && p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().FullName == "Il2CppSystem.Collections.Generic.List`1");
        var list = show.GetValue(song)!;
        var chartType = show.PropertyType.GenericTypeArguments[0];
        var add = show.PropertyType.GetMethod("Add")!; var clear = show.PropertyType.GetMethod("Clear")!;
        var count = show.PropertyType.GetProperty("Count")!;
        var chartMetaProperty = chartType.GetProperties().Single(p => p.PropertyType == typeof(ChartRecord));
        object Chart(uint status,PlayModeType chartMode)
        {
            var chart = Activator.CreateInstance(chartType)!;
            chartType.GetMethod("InitWithMode")!.Invoke(chart,new object[] { chartMode });
            var meta = (ChartRecord)chartMetaProperty.GetValue(chart)!;
            meta.RankStatus = status; meta.NoteCount = 100; meta.Version = "Lv.5";
            songMeta.Mode |= meta.ChartMode;
            return chart;
        }
        var stable = Chart(2,PlayModeType.Key); var beta = Chart(1,PlayModeType.Taiko);
        var alpha = Chart(0,PlayModeType.Key); var unknown = Chart(10,PlayModeType.Key);
        foreach(var chart in new[] { stable,beta,alpha,unknown }) add.Invoke(list,new[] { chart });
        mode.SetValue(filter,PlayModeType.Max);
        for (var mask = 0; mask <= 7; mask++)
        {
            Plugin.SetSelection((StatusMask)mask);
            Check((bool)adapter.Contains.Invoke(filter,new[] { song })! == (mask != 0),"Native IsContain mask="+mask);
            using (adapter.FilterDisplay(song))
            {
                var expected = mask == 7 ? 4 : System.Numerics.BitOperations.PopCount((uint)mask);
                Check((int)count.GetValue(list)! == expected,"Native display subset mask="+mask);
            }
            Check((int)count.GetValue(list)! == 4,"Restore complete display list mask="+mask);
        }
        Plugin.SetSelection(StatusMask.Stable); mode.SetValue(filter,PlayModeType.Taiko);
        Check(!(bool)adapter.Contains.Invoke(filter,new[] { song })!,"Require same chart to match mode and status");
        mode.SetValue(filter,PlayModeType.Key);
        Check((bool)adapter.Contains.Invoke(filter,new[] { song })!,"Stable chart matches selected Key mode");
        ((ChartRecord)chartMetaProperty.GetValue(stable)!).RankStatus = 1;
        Check(!(bool)adapter.Contains.Invoke(filter,new[] { song })!,"Observe live cached status updates");
        clear.Invoke(list,null); add.Invoke(list,new[] { unknown }); mode.SetValue(filter,PlayModeType.Max);
        Check(!(bool)adapter.Contains.Invoke(filter,new[] { song })!,"Unknown never passes Stable");
        Plugin.SetSelection(StatusMask.Alpha);
        Check(!(bool)adapter.Contains.Invoke(filter,new[] { song })!,"Unknown never passes Alpha");
        Plugin.SetSelection(StatusMask.All);
        Check((bool)adapter.Contains.Invoke(filter,new[] { song })!,"All preserves unknown charts");
    }

    private void TestControls()
    {
        captureCamera = new GameObject("VerificationCamera").AddComponent<Camera>();
        captureCamera.transform.position = new Vector3(0,0,-10); captureCamera.orthographic = true; captureCamera.orthographicSize = Height/2f;
        captureCamera.targetTexture = new RenderTexture(Width,Height,24);
        captureCamera.clearFlags = CameraClearFlags.SolidColor; captureCamera.backgroundColor = new Color(.04f,.05f,.07f,1);
        UnityEngine.Object.DontDestroyOnLoad(captureCamera.gameObject);
        var canvas = new GameObject("VerificationCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = captureCamera; canvas.planeDistance = 1; canvas.sortingOrder = 32767;
        UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440,900);
        scaler.matchWidthOrHeight = 0;
        var front = UnityEngine.Object.FindObjectOfType<FrontLayerManager>();
        Check(front != null,"Found real FrontLayerManager from installed game scene");
        var prefab = front!.prefabs[(int)FrontLayerManager.PrefabType.SearchAdv];
        Check(prefab.GetComponent<PanelSearchSong>() != null,"Using actual shipped advanced-search prefab");
        var root = UnityEngine.Object.Instantiate(prefab,canvas.transform,false);
        realPanel = root.GetComponent<PanelSearchSong>();
        realPanel.input.text = "Search text remains editable";
        // Deliberately do NOT call Show/Attach. The shipping controller must
        // discover this normally enabled prefab even when Show was inlined.
        root.SetActive(true);
    }

    private void ValidateRealPanel()
    {
        var panel = realPanel ?? throw new Exception("Real search panel was not instantiated");
        var root = panel.gameObject;
        var input = panel.input;
        var controls = input.transform.Find(SearchFilterUi.RootName);
        Check(controls != null,"Shipping controller discovered actual native panel without Show hook");
        var originalOffset = input.textViewport.offsetMax;
        SearchFilterUi.Attach(panel); SearchFilterUi.Attach(panel);
        Check(input.transform.childCount > 1 && input.textViewport.offsetMax == originalOffset,"Repeated discovery does not duplicate or shrink text viewport");
        var toggles = root.GetComponentsInChildren<Toggle>(true);
        Check(toggles.Length == 3,"Three native status checkboxes");
        var events = EventSystem.current;
        var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
        toggles[0].OnPointerClick(pointer);
        Check(Plugin.Selection == (StatusMask.Alpha | StatusMask.Beta),"Checkbox persists combined selection");
        toggles[1].isOn = false; toggles[2].isOn = false;
        Check(Plugin.Selection == StatusMask.None,"Unchecking all yields empty selection");
        var button = controls!.GetComponentInChildren<Button>(true); button.onClick.Invoke();
        Check(Plugin.Selection == StatusMask.All,"All button resets status selection");
        var controlRect = controls.GetComponent<RectTransform>();
        var inputRect = input.GetComponent<RectTransform>();
        Check(input.textViewport.rect.width >= 200,"Actual search retains at least 200 units of editable text");
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        controlRect.GetWorldCorners(corners);
        foreach(var corner in corners)
        {
            Check(inputRect.rect.Contains((Vector2)inputRect.InverseTransformPoint(corner)),"Status controls fit inside actual search input");
            var point = captureCamera!.WorldToViewportPoint(corner);
            Check(point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1,"Settled controls fit inside the captured viewport");
        }
        foreach(var label in controls.GetComponentsInChildren<TextMeshProUGUI>())
            Check(label.GetPreferredValues(label.text).x <= label.rectTransform.rect.width,"Full one-line status label fits: "+label.text);
        Check(input.text == "Search text remains editable","Attaching controls preserves search text");
        var text = new string('W',120); input.text = text;
        Check(input.text == text,"Long search text remains editable");
        input.ActivateInputField();
        Check(input.interactable,"Search field remains interactable");
        var selected = new BaseEventData(events);
        toggles[0].OnSelect(selected);
        toggles[0].OnSubmit(selected);
        Check(Plugin.Selection == (StatusMask.Alpha|StatusMask.Beta),"Native keyboard submit toggles Stable");
        button.onClick.Invoke();
        input.text = "Search";
        WriteResult();
    }
}
