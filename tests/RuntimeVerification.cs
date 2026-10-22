using System.Reflection;
using System.Text.Json;
using BetterMalodyV.Shared;
using HarmonyLib;
using Malody.Chart;
using Malody.DB;
using Malody.Scene;
using Malody.Scene.Panel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterMalodyV.Plugin;

// Compiled only for the isolated verification host; absent from shipping DLLs.
public sealed class RuntimeVerification : MonoBehaviour
{
    public RuntimeVerification(IntPtr pointer) : base(pointer) { }
    private bool ran;
    private int frames;
    private static string Root => Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_OUTPUT")!;
    private readonly List<string> checks = new();
    private Camera? captureCamera;
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
            frames++;
            if (frames == 8 && captureCamera is not null)
            {
                Canvas.ForceUpdateCanvases(); captureCamera.Render();
                RenderTexture.active = captureCamera.targetTexture;
                var image = new Texture2D(1440,900,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1440,900),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(Root,"search-filter-fixture.png"),ImageConversion.EncodeToPNG(image));
                RenderTexture.active = null;
            }
            if (frames > 24) Application.Quit();
            return;
        }
        ran = true;
        try
        {
            TestNativeData(); TestControls();
            File.WriteAllText(Path.Combine(Root,"result.json"),JsonSerializer.Serialize(new { passed = true, checks },new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(Root,"result.json"),JsonSerializer.Serialize(new { passed = false, error = ex.ToString(), checks }));
        }
    }
    private void Check(bool condition,string label) { if (!condition) throw new Exception(label); checks.Add(label); }
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
        captureCamera.transform.position = new Vector3(0,0,-10); captureCamera.orthographic = true; captureCamera.orthographicSize = 450;
        captureCamera.targetTexture = new RenderTexture(1440,900,24);
        captureCamera.clearFlags = CameraClearFlags.SolidColor; captureCamera.backgroundColor = new Color(.04f,.05f,.07f,1);
        UnityEngine.Object.DontDestroyOnLoad(captureCamera.gameObject);
        var canvas = new GameObject("VerificationCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = captureCamera; canvas.planeDistance = 1; canvas.sortingOrder = 32767;
        UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        var root = new GameObject("Search fixture"); root.SetActive(false);
        var rect = root.AddComponent<RectTransform>(); rect.SetParent(canvas.transform,false);
        rect.sizeDelta = new Vector2(1180,450);
        root.AddComponent<Image>().color = new Color(.09f,.11f,.14f,1);
        var panel = root.AddComponent<PanelSearchSong>();
        var side = new GameObject("side").AddComponent<RectTransform>(); side.SetParent(rect,false); side.sizeDelta = new Vector2(1180,335);
        var middle = new GameObject("middle").AddComponent<RectTransform>(); middle.SetParent(side,false);
        middle.sizeDelta = new Vector2(1100,190);
        var input = new GameObject("input").AddComponent<TMP_InputField>(); input.transform.SetParent(rect,false);
        var label = new GameObject("text").AddComponent<TextMeshProUGUI>(); label.transform.SetParent(input.transform,false);
        label.font = TMP_Settings.defaultFontAsset; input.textComponent = label; panel.input = input;
        SearchFilterUi.Attach(panel); SearchFilterUi.Attach(panel);
        Check(middle.childCount == 1,"Repeated panel open does not duplicate status controls");
        var toggles = root.GetComponentsInChildren<Toggle>(true);
        Check(toggles.Length == 3,"Three native status checkboxes");
        toggles[0].isOn = false;
        Check(Plugin.Selection == (StatusMask.Alpha | StatusMask.Beta),"Checkbox persists combined selection");
        toggles[1].isOn = false; toggles[2].isOn = false;
        Check(Plugin.Selection == StatusMask.None,"Unchecking all yields empty selection");
        var button = root.GetComponentInChildren<Button>(true); button.onClick.Invoke();
        Check(Plugin.Selection == StatusMask.All,"All button resets status selection");
        root.SetActive(true);
    }
}
