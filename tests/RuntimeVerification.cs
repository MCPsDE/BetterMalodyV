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
using UnityEngine.SceneManagement;

namespace BetterMalodyV.Plugin;

// Compiled only for the isolated verification host; absent from shipping DLLs.
public sealed class RuntimeVerification : MonoBehaviour
{
    public RuntimeVerification(IntPtr pointer) : base(pointer) { }
    private bool ran;
    private bool benchmarkDone;
    private float captureAt = float.PositiveInfinity;
    private float exitAt = float.PositiveInfinity;
    private static string Root => Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_OUTPUT")!;
    private readonly List<string> checks = new();
    private Camera? captureCamera;
    private Canvas? searchCanvas;
    private PanelSearchSong? realPanel;
    private long refreshMilliseconds;
    private int stressSongs;
    private int cachedRecords;
    private long indexBuildMilliseconds;
    private int realSongSamples,realStatusMatches;
    private static int scanAttempts;
    private static bool blockScans;
    private float roomCaptureAt=float.PositiveInfinity;
    private UnityEngine.UI.Button? roomUpdateButton;
    private float updateCaptureAt=float.PositiveInfinity;
    private int updateCapture;
    private GameUpdateDialog? updateDialog;
    private readonly string[] updateStates={"download","verifying","ready","failed","unknown"};
    private float replayCaptureAt=float.PositiveInfinity;
    private UnityEngine.UI.Button? replayAction;
    private UnityEngine.UI.Button? replayAnalysisAction;
    private float replayAnalysisCaptureAt=float.PositiveInfinity;
    private int replayAnalysisCapture;
    private static int Width => int.TryParse(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_WIDTH"),out var width) ? width : 1440;
    private static int Height => int.TryParse(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_HEIGHT"),out var height) ? height : 900;
    public static void Install(Plugin plugin, Harmony harmony)
    {
        if (string.IsNullOrWhiteSpace(Root)) throw new InvalidOperationException("Verification output required");
        harmony.Patch(AccessTools.Method(typeof(SceneLogo),"Start"), prefix: new HarmonyMethod(typeof(RuntimeVerification),nameof(SkipLogo)));
        harmony.Patch(AccessTools.Method(typeof(SceneRoom),"Start"), prefix: new HarmonyMethod(typeof(RuntimeVerification),nameof(SkipLogo)));
        plugin.AddComponent<RuntimeVerification>();
        harmony.Patch(Plugin.Adapter.ScanFiles,prefix:new HarmonyMethod(typeof(RuntimeVerification),nameof(TrackScan)));
    }
    private static bool TrackScan() { if (!blockScans) return true; scanAttempts++; return false; }
    private static bool SkipLogo() => false;
    public void Update()
    {
        if (ran)
        {
            if (Time.unscaledTime >= captureAt && captureCamera is not null)
            {
                if (!benchmarkDone)
                {
                    benchmarkDone=true;
                    try { TestLargeLibrary(); }
                    catch (Exception ex) { WriteResult(ex); exitAt=Time.unscaledTime+.2f; captureAt=float.PositiveInfinity; return; }
                    // Native fixture allocation can consume a long frame.
                    // Let popup animation/layout advance before measuring the UI.
                    captureAt=Time.unscaledTime+1.2f;
                    return;
                }
                captureAt = float.PositiveInfinity;
                try { ValidateRealPanel(); }
                catch (Exception ex) { WriteResult(ex); }
                Canvas.ForceUpdateCanvases(); captureCamera.Render();
                RenderTexture.active = captureCamera.targetTexture;
                var image = new Texture2D(Width,Height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,Width,Height),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(Root,"search-filter-fixture.png"),ImageConversion.EncodeToPNG(image));
                RenderTexture.active = null;
                if(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_ROOM_VISUAL")=="1")
                {
                    try { BeginRoomCapture();roomCaptureAt=Time.unscaledTime+1.2f; }
                    catch(Exception ex){WriteResult(ex);exitAt=Time.unscaledTime+.2f;}
                }
                else if(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_UPDATE_VISUAL")=="1")BeginUpdateCapture();
                else if(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_REPLAY_VISUAL")=="1")BeginReplayCapture();
                else exitAt = Time.unscaledTime+.2f;
            }
            if(Time.unscaledTime>=roomCaptureAt && captureCamera is not null)
            {
                roomCaptureAt=float.PositiveInfinity;
                try
                {
                    PrepareRoomCapture();
                    var label=roomUpdateButton!.GetComponentInChildren<TextMeshProUGUI>();
                    Check(label.GetPreferredValues(label.text).x+24<=roomUpdateButton.GetComponent<RectTransform>().rect.width,"Actual room update label fits at native font size");
                    Canvas.ForceUpdateCanvases();captureCamera.Render();RenderTexture.active=captureCamera.targetTexture;
                    var bitmap=new Texture2D(Width,Height,TextureFormat.RGB24,false);bitmap.ReadPixels(new Rect(0,0,Width,Height),0,0);bitmap.Apply();
                    File.WriteAllBytes(Path.Combine(Root,"room-update-fixture.png"),ImageConversion.EncodeToPNG(bitmap));RenderTexture.active=null;
                    WriteResult();
                }
                catch(Exception ex){WriteResult(ex);}
                if(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_UPDATE_VISUAL")=="1")BeginUpdateCapture();
                else if(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_REPLAY_VISUAL")=="1")BeginReplayCapture();
                else exitAt=Time.unscaledTime+.2f;
            }
            if(Time.unscaledTime>=updateCaptureAt && captureCamera is not null)
            {
                try {CaptureUpdate();}
                catch(Exception ex){WriteResult(ex);updateCaptureAt=float.PositiveInfinity;exitAt=Time.unscaledTime+.2f;}
            }
            if(Time.unscaledTime>=replayCaptureAt && captureCamera is not null)
            {
                replayCaptureAt=float.PositiveInfinity;
                try
                {
                    Canvas.ForceUpdateCanvases();var label=replayAction!.GetComponentInChildren<TextMeshProUGUI>();
                    Check(label.GetPreferredValues(label.text).x+24<=replayAction.GetComponent<RectTransform>().rect.width,"Complete reenactment action fits original native font");
                    captureCamera.Render();RenderTexture.active=captureCamera.targetTexture;
                    var bitmap=new Texture2D(Width,Height,TextureFormat.RGB24,false);bitmap.ReadPixels(new Rect(0,0,Width,Height),0,0);bitmap.Apply();
                    File.WriteAllBytes(Path.Combine(Root,"replay-action.png"),ImageConversion.EncodeToPNG(bitmap));RenderTexture.active=null;WriteResult();
                    BeginReplayAnalysisCapture();
                }
                catch(Exception ex){WriteResult(ex);}
            }
            if(Time.unscaledTime>=replayAnalysisCaptureAt && captureCamera is not null)
            {
                replayAnalysisCaptureAt=float.PositiveInfinity;
                try
                {
                    var page=ReplayAnalysisView.TestRoot!;
                    var labels=page.GetComponentsInChildren<TextMeshProUGUI>(true);
                    var buttons=page.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                    var tabs=buttons.Where(button=>button.name.StartsWith("Tab ",StringComparison.Ordinal)).ToArray();
                    Check(tabs.Length==4,"Replay analysis provides four sections");
                    foreach(var tab in tabs)
                    {
                        var text=tab.GetComponentInChildren<TextMeshProUGUI>();
                        Check(text!.GetPreferredValues(text.text).x+20<=tab.GetComponent<RectTransform>().rect.width,"Full analysis tab label fits: "+text.text);
                    }
                    if(replayAnalysisCapture==0)Check(labels.Any(label=>label.text.Contains("总偏移") || label.text.Contains("Total offset")),"Overview displays signed offset statistics");
                    if(replayAnalysisCapture==1)Check(labels.Any(label=>label.text.Contains("逐轨偏移") || label.text.Contains("Offset by track")),"Track section is rendered");
                    if(replayAnalysisCapture==2)
                    {
                        Check(labels.Any(label=>label.text.Contains("按压时长") || label.text.Contains("Press duration")),"Press section is rendered");
                        Check(labels.Any(label=>label.text=="500+ ms") && labels.Any(label=>label.text=="1000+ ms"),"Press charts label their overflow ranges");
                        var histograms=page.GetComponentsInChildren<RectTransform>(true).Where(rect=>rect.name=="Histogram" && rect.gameObject.activeInHierarchy).ToArray();
                        Check(histograms.Length>=2,"Duration and interval histograms are rendered");
                        var durationPlot=ReplayAnalysisView.TestPlots.Single(plot=>plot.Id=="PressDuration");
                        Check(durationPlot.Points.Count==51 && durationPlot.Points.Any(point=>Math.Abs(point.X-75)<.001)
                            && durationPlot.MaximumX==510 && durationPlot.ConnectPoints,
                            "Duration distribution uses fine 10 ms point sampling");
                        var intervalPlot=ReplayAnalysisView.TestPlots.Single(plot=>plot.Id=="PressInterval");
                        Check(intervalPlot.Points.Any(point=>Math.Abs(point.X-1005)<.001) && intervalPlot.MaximumX==1010,
                            "Interval overflow is drawn at its fine representative midpoint");
                        Check(histograms.All(graph=>graph.GetComponentInChildren<RawImage>(true).texture),
                            "Distribution plots render as native raster textures");
                    }
                    if(replayAnalysisCapture==3)
                    {
                        Check(labels.Any(label=>label.text.Contains("Baka-e")),"Judgement section includes CVT profile results");
                        Check(labels.Any(label=>label.text=="100%") && labels.Any(label=>label.text=="75%")
                            && labels.Any(label=>label.text=="50%") && labels.Any(label=>label.text=="25%")
                            && labels.Any(label=>label.text=="0%"),"CVT chart shows the reference percentage grid");
                        var timeline=page.GetComponentsInChildren<RectTransform>(true).First(rect=>rect.name=="AccuracyTimeline" && rect.gameObject.activeInHierarchy);
                        var scatter=ReplayAnalysisView.TestPlots.Single(plot=>plot.Id=="CVT:Baka");
                        Check(scatter.Points.Count==32 && !scatter.ConnectPoints && Math.Abs(scatter.Points[0].X-5000)<.001
                            && Math.Abs(scatter.Points[^1].X-17750)<.001 && ReplayPlotTexture.NormalPointRadius==2.2f,
                            "CVT chart draws one fine, unconnected point per object at source timestamps");
                        Check(timeline.GetComponentInChildren<RawImage>(true).texture,
                            "CVT scatter graph renders to a single native image");
                    }
                    Canvas.ForceUpdateCanvases();captureCamera.Render();RenderTexture.active=captureCamera.targetTexture;
                    var bitmap=new Texture2D(Width,Height,TextureFormat.RGB24,false);bitmap.ReadPixels(new Rect(0,0,Width,Height),0,0);bitmap.Apply();
                    var names=new[]{"overview","tracks","presses","profiles"};
                    File.WriteAllBytes(Path.Combine(Root,"replay-analysis-"+names[replayAnalysisCapture]+".png"),ImageConversion.EncodeToPNG(bitmap));RenderTexture.active=null;
                    replayAnalysisCapture++;
                    if(replayAnalysisCapture<names.Length)
                    {
                        tabs[replayAnalysisCapture].onClick.Invoke();
                        replayAnalysisCaptureAt=Time.unscaledTime+.6f;
                        return;
                    }
                    var back=buttons.Single(button=>button.name=="Back");back.onClick.Invoke();
                    Check(!ReplayAnalysisView.IsOpen,"Back closes the full-screen analysis page");
                    ReplayAnalysisView.TestCamera=null;WriteResult();
                }
                catch(Exception ex){WriteResult(ex);}
                exitAt=Time.unscaledTime+.2f;
            }
            if (Time.unscaledTime >= exitAt) Application.Quit();
            return;
        }
        ran = true;
        try
        {
            TestNativeData(); TestRoomBindings(); RoomRuntimeTests.Run(Check); ReplayRuntimeTests.Run(Check); TestControls();
            captureAt = Time.unscaledTime+1.2f;
        }
        catch (Exception ex)
        {
            WriteResult(ex);
            exitAt = Time.unscaledTime+.2f;
        }
    }
    private void Check(bool condition,string label) { if (!condition) throw new Exception(label); checks.Add(label); }
    private void BeginUpdateCapture()
    {
        try
        {
            searchCanvas!.gameObject.SetActive(false);
            Check(GameUpdateController.CanRestart(),"Actual menu permits update presentation and restart");
            foreach(var name in new[]{"ScenePlay.UpdateFixture","SceneEdit.UpdateFixture"})
            {
                var scene=SceneManager.CreateScene(name,default(CreateSceneParameters));var marker=new GameObject("Update safety fixture");SceneManager.MoveGameObjectToScene(marker,scene);
                Check(!GameUpdateController.CanRestart(),"Actual "+name+" defers update presentation and restart");
                marker.SetActive(false);UnityEngine.Object.Destroy(marker);
            }
            updateDialog=new();
            try
            {
                GameUpdateDialog.TestCreateFailure=()=>throw new IOException("Simulated native dialog setup failure");
                try {updateDialog.Render(new(GameUpdateStage.Ready,"1.1.2"),true,3);}
                catch(IOException) { }
                Check(!updateDialog.TestPanel && !updateDialog.ReadyPresented,"Failed native popup creation cannot enable automatic restart");
            }
            finally {GameUpdateDialog.TestCreateFailure=null;}
            updateDialog.Render(new(GameUpdateStage.Checking),true);
            Check(!updateDialog.TestPanel,"No game popup for a quiet update check");
            ApplyUpdateState();updateCaptureAt=Time.unscaledTime+1.2f;
        }
        catch(Exception ex){WriteResult(ex);exitAt=Time.unscaledTime+.2f;}
    }
    private void ApplyUpdateState()
    {
        var value=updateCapture switch
        {
            0=>new GameUpdateProgress(GameUpdateStage.Downloading,"1.1.2",70*1048576,200*1048576),
            1=>new GameUpdateProgress(GameUpdateStage.Verifying,"1.1.2"),
            2=>new GameUpdateProgress(GameUpdateStage.Ready,"1.1.2"),
            3=>new GameUpdateProgress(GameUpdateStage.Failed,"1.1.2"),
            _=>new GameUpdateProgress(GameUpdateStage.Downloading,"1.1.2",70*1048576)
        };
        updateDialog!.Render(value,true,3);
        var panel=updateDialog.TestPanel!;Check(panel,"Uses actual native update dialog: "+updateStates[updateCapture]);
        var canvas=panel.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=captureCamera;canvas.planeDistance=1;
    }
    private void CaptureUpdate()
    {
        var panel=updateDialog!.TestPanel!;
        var rect=panel.textOK.transform.parent.GetComponent<RectTransform>();
        Check(panel.textOK.GetPreferredValues(panel.textOK.text).x+24<=rect.rect.width,"Update dialog command fits completely: "+updateStates[updateCapture]);
        if(updateCapture==0)
        {
            Check(panel.desc.text.Contains("35") && Math.Abs(updateDialog.TestFill!.anchorMax.x-.35f)<.001,"Actual download percentage drives text and progress bar");
            Check(panel.desc.GetPreferredValues(panel.desc.text).y<=panel.desc.rectTransform.rect.height,"Complete download copy fits original native font");
        }
        if(updateCapture==2)Check(updateDialog.TestFill!.anchorMax.x==1 && panel.desc.text.Contains("3"),"Ready dialog shows full progress and restart countdown");
        if(updateCapture==3)Check(!updateDialog.TestFill!.gameObject.activeSelf,"Failed update hides progress and permits continuing the game");
        if(updateCapture==4)Check(!panel.desc.text.Contains('%'),"Unknown length shows received bytes without a fabricated percentage");
        Canvas.ForceUpdateCanvases();captureCamera!.Render();RenderTexture.active=captureCamera.targetTexture;
        var bitmap=new Texture2D(Width,Height,TextureFormat.RGB24,false);bitmap.ReadPixels(new Rect(0,0,Width,Height),0,0);bitmap.Apply();
        File.WriteAllBytes(Path.Combine(Root,"game-update-"+updateStates[updateCapture]+".png"),ImageConversion.EncodeToPNG(bitmap));RenderTexture.active=null;
        updateCapture++;
        if(updateCapture<updateStates.Length){ApplyUpdateState();updateCaptureAt=Time.unscaledTime+.4f;return;}
        updateDialog.Close();updateDialog.Render(new(GameUpdateStage.Downloading,"1.1.2",50,100),true);
        Check(!updateDialog.TestPanel,"Hiding a download does not repeatedly reopen the popup");
        updateDialog.Render(new(GameUpdateStage.Ready,"1.1.2"),false);
        Check(!updateDialog.TestPanel && !updateDialog.ReadyPresented,"Play or editing defers the update popup and readiness notice");
        updateDialog.Render(new(GameUpdateStage.Ready,"1.1.2"),true,3);
        Check(updateDialog.TestPanel,"Ready popup appears when safely back in a menu");
        foreach(var group in updateDialog.TestPanel!.GetComponentsInParent<CanvasGroup>())group.alpha=1;
        updateDialog.Render(new(GameUpdateStage.Ready,"1.1.2"),true,3);
        Check(updateDialog.ReadyPresented,"Visible ready notice enables restart countdown");
        updateDialog.Close();WriteResult();updateCaptureAt=float.PositiveInfinity;
        if(Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_REPLAY_VISUAL")=="1")BeginReplayCapture();else exitAt=Time.unscaledTime+.2f;
    }
    private void BeginReplayCapture()
    {
        try
        {
            searchCanvas!.gameObject.SetActive(false);
            var front=UnityEngine.Object.FindObjectOfType<FrontLayerManager>();
            var panel=front!.Instantiate(FrontLayerManager.PrefabType.ScoreDetail,false).GetComponent<PanelScoreDetail>();
            panel.Show(new ScoreRecord{Score=123456,Combo=200,Acc=98.5f,Level=2,Rank=1,Best=190,Cool=5,Good=3,Miss=2,Time=1782000000});
            var owner=ReplayRunController.Current!;owner.Api.SetLocalPath(panel,Path.Combine(Root,"own-replay-fixture.mrv"));
            panel.areaRep.SetActive(true);panel.btnCancel.SetActive(false);panel.iconShare.SetActive(true);
            File.WriteAllText(Path.Combine(Root,"replay-ui-structure.json"),JsonSerializer.Serialize(panel.GetComponentsInChildren<RectTransform>(true).Select(r=>new{name=r.name,parent=r.parent?.name,width=r.rect.width,height=r.rect.height,x=r.anchoredPosition.x,y=r.anchoredPosition.y,legacy=r.GetComponent<Text>()?.text,tmp=r.GetComponent<TextMeshProUGUI>()?.text}),new JsonSerializerOptions{WriteIndented=true}));
            replayAction=owner.Attach(panel);Check(replayAction,"Local chart score details expose reenactment action beside native replay controls");
            replayAnalysisAction=panel.areaRep.transform.Find("BetterMalodyV.ReplayAnalysis")?.GetComponent<UnityEngine.UI.Button>();
            Check(replayAnalysisAction,"Local chart score details expose full-screen analysis action");
            Check(owner.Attach(panel)==replayAction,"Reopening discovery does not duplicate replay action");
            var canvas=panel.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=captureCamera;canvas.planeDistance=1;
            replayCaptureAt=Time.unscaledTime+1.2f;
        }
        catch(Exception ex){WriteResult(ex);exitAt=Time.unscaledTime+.2f;}
    }
    private void BeginReplayAnalysisCapture()
    {
        var chart=new List<ReplayChartNote>();var actions=new List<ReplayInputAction>();var hits=new List<ReplayHitEvent>();
        for(var index=0;index<32;index++)
        {
            var lane=index%4;var time=5000+index*250+(index>=16?5000:0);
            chart.Add(new(time,lane));actions.Add(new(time+6,lane,true));actions.Add(new(time+74,lane,false));
            hits.Add(new(time+6,lane,ReplayAnalysisEngine.GameJudgementFlags.Best));
        }
        var analysis=ReplayAnalysisEngine.Analyze(chart,actions,hits,4);
        var session=new ReplayAnalysisSession("Replay analysis fixture","Local chart","Normal","fixture.mrv","Key",123456,200,98.5,"S",analysis);
        ReplayAnalysisView.TestCamera=captureCamera;
        ReplayAnalysisView.Open(replayAnalysisAction!.GetComponentInChildren<TextMeshProUGUI>(),session);
        Check(ReplayAnalysisView.IsOpen,"Analysis action opens the full-screen report");
        var tabs=ReplayAnalysisView.TestRoot!.GetComponentsInChildren<UnityEngine.UI.Button>(true)
            .Where(button=>button.name.StartsWith("Tab ",StringComparison.Ordinal)).ToArray();
        foreach(var tab in tabs)tab.onClick.Invoke();
        Check(tabs.All(tab=>tab.gameObject.activeInHierarchy),"All analysis sections remain reachable from the top bar");
        tabs[0].onClick.Invoke();Canvas.ForceUpdateCanvases();
        replayAnalysisCaptureAt=Time.unscaledTime+1.2f;
    }
    private void BeginRoomCapture()
    {
        searchCanvas!.gameObject.SetActive(false);
        RoomChartController.Current!.enabled=false;
        SceneManager.LoadScene("SceneRoom",LoadSceneMode.Additive);
    }
    private void PrepareRoomCapture()
    {
        var owner=UnityEngine.Object.FindObjectOfType<SceneRoom>();Check(owner is not null,"Actual native Room scene loads for isolated visual fixture");
        var controller=RoomChartController.Current!;controller.enabled=false;
        controller.TestContext(owner!,new RoomInfo{Roomid=1,Hostid=1,ChartTitle="Visual fixture",ChartHash=""});
        owner!.panelSong.title.text="Sample song (visual fixture)";owner.panelSong.artist.text="Room update preview";
        owner.panelSong.iconDL.SetActive(false);roomUpdateButton=controller.TestAction(owner);
        foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
            if(canvas.gameObject.scene.name=="SceneRoom") {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=captureCamera;canvas.planeDistance=1;}
    }
    private void TestRoomBindings()
    {
        var controller=RoomChartController.Current??throw new Exception("Room feature bindings did not initialize");
        var api=controller.Api;
        var info=new RoomInfo {Roomid=1,Hostid=200,ChartId=123,SongId=456,ChartHash=new string('a',32),ChartTitle="Test chart",ChartDiff="Lv.5"};
        var record=api.Record(info);
        Check((uint)RoomGameApi.Get(record,"yug")==123 && (uint)RoomGameApi.Get(record,"yuh")==456,"Official download record binds chart/song identity");
        Check((string)RoomGameApi.Get(record,"yuj")=="","Host query requests latest official version by ID");
        var framework=typeof(PlayModeType).Assembly;
        var onlineInfo=framework.GetTypes().Single(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Any(m=>m.Name=="get_Self"));
        var self=onlineInfo.GetMethod("get_Self")!.Invoke(RoomGameApi.Singleton(onlineInfo),null)!;
        var user=self.GetType().GetProperties().Single(p=>p.PropertyType==typeof(uint));var saved=user.GetValue(self);
        try
        {
            user.SetValue(self,(uint)200);Check(api.IsHost(info),"Host button uses actual local user ID");
            user.SetValue(self,(uint)201);Check(!api.IsHost(info),"Room member cannot invoke host update");
        }
        finally{user.SetValue(self,saved);}
        var method=api.OnlineType.GetMethods().Single(m=>m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType==api.RecordType && m.ReturnType.IsGenericType && m.ReturnType.GenericTypeArguments[0].FullName=="Malody.JSON.RespDownload");
        var pack=Activator.CreateInstance(method.ReturnType.GenericTypeArguments[0])!;
        var completed=Activator.CreateInstance(method.ReturnType,new[]{pack})!;
        var awaitable=new NativePending(completed);
        Check(awaitable.Poll() && awaitable.Result is not null,"Actual native UniTask completion/results consumed without blocking");
        Check(api.ShareReceive.DeclaringType==typeof(Malody.Scene.MP.WidgetMPShare)&&api.ShareReceive.Name=="rxb"&&api.ShareEvent.Name=="rwy",
            "Share hook intercepts the native TCP notification before async/download enqueue, excluding skins");
        Check(Directory.GetParent(api.ChartRoot)!.FullName==BepInEx.Paths.GameRootPath.TrimEnd('\\'),"Room import destination is isolated game chart directory");
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void WriteResult(Exception? error = null) => File.WriteAllText(Path.Combine(Root,"result.json"),
        JsonSerializer.Serialize(new { passed = error is null,error = error?.ToString(),checks,
            performance = new { cachedRecords,indexBuildMilliseconds,realSongSamples,realStatusMatches,unloadedSongs = stressSongs,refreshMilliseconds,scanAttempts } },new JsonSerializerOptions { WriteIndented = true }));
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
        var modeProperty=chartType.GetProperties().Single(p=>p.PropertyType==typeof(PlayModeType));
        var mappingChart=Chart(2,PlayModeType.Key);
        foreach(var fileMode in new uint[] { 1,0x10,0x20,0x40,0x10000,0x20000,2,8,0x80,0x100,4,0x200,0x400,0x800,0x1000,0x2000,0x4000,0x8000 })
        {
            ((ChartRecord)chartMetaProperty.GetValue(mappingChart)!).ChartMode=fileMode;
            Check((int)(PlayModeType)modeProperty.GetValue(mappingChart)! == ChartStatusIndex.ToPlayMode(fileMode),"Cached mode mapping matches actual game format "+fileMode);
        }
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

    private void TestLargeLibrary()
    {
        var adapter = Plugin.Adapter;
        cachedRecords=adapter.StatusStore.Index.RecordCount;
        indexBuildMilliseconds=adapter.StatusStore.LastBuildMilliseconds;
        if (Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_METADATA")=="1")
            Check(adapter.StatusStore.Ready && cachedRecords>0,"Background index reads real copied chart database without loading charts");
        var singleton = adapter.ManagerType.BaseType!.GetProperties(BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy)
            .Single(p=>p.PropertyType==adapter.ManagerType && !p.CanWrite);
        var manager = singleton.GetValue(null)!;
        var properties = adapter.ManagerType.GetProperties();
        var rawProperty = properties.Single(p=>p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().FullName=="Il2CppSystem.Collections.Generic.Dictionary`2"
            && p.PropertyType.GenericTypeArguments[1]==adapter.SongType);
        var raw = rawProperty.GetValue(manager)!;
        var metaProperty = adapter.SongType.GetProperties().Single(p=>p.PropertyType==typeof(SongRecord));
        if (Environment.GetEnvironmentVariable("BETTER_MALODYV_TEST_METADATA")=="1")
        {
            var at=adapter.ManagerType.GetMethod("GetSongAt")!;
            for(var i=0;i<1000;i++)
            {
                var real=at.Invoke(manager,new object[] { i });
                if(real is null) break;
                var realMeta=(SongRecord)metaProperty.GetValue(real)!; realSongSamples++;
                if(adapter.StatusStore.Index.Matches(realMeta.Path,(int)PlayModeType.Max,StatusMask.Stable|StatusMask.Beta)) realStatusMatches++;
            }
            Check(realSongSamples>0 && realStatusMatches>0,"Actual SongRecord paths match background ChartRecord folder index");
        }
        var rawClear = rawProperty.PropertyType.GetMethod("Clear")!; rawClear.Invoke(raw,null);
        var rawAdd = rawProperty.PropertyType.GetMethod("Add")!;
        var index = new ChartStatusIndex(); stressSongs=20_000;
        for(var i=0;i<stressSongs;i++)
        {
            var song=Activator.CreateInstance(adapter.SongType)!;
            var meta=(SongRecord)metaProperty.GetValue(song)!;
            meta.Path=$"E:/BetterMalodyV-never-read/Song{i}/"; meta.Mode=0x40; meta.Title="Stress fixture";
            index.Add(meta.Path+"chart.mc",0x40,(uint)(i%3));
            rawAdd.Invoke(raw,new object[] { (ulong)(i+1),song });
        }
        var originalIndex = adapter.StatusStore.Index;
        adapter.StatusStore.SetTestIndex(index);
        var filter = properties.Single(p=>p.PropertyType==adapter.Contains.DeclaringType && !p.CanWrite).GetValue(manager)!;
        filter.GetType().GetProperties().Single(p=>p.PropertyType==typeof(PlayModeType)).SetValue(filter,PlayModeType.Max);
        try
        {
            Plugin.SetSelection(StatusMask.Stable); blockScans=true; scanAttempts=0;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            Check(adapter.RefreshAfterIndex(),"Actual native RefreshDisplayList executes on unloaded large library");
            refreshMilliseconds=watch.ElapsedMilliseconds;
            var listProperty=properties.Single(p=>p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().FullName=="Il2CppSystem.Collections.Generic.List`1"
                && p.PropertyType.GenericTypeArguments[0]==adapter.SongType);
            var list=listProperty.GetValue(manager)!;
            var count=(int)listProperty.PropertyType.GetProperty("Count")!.GetValue(list)!;
            Check(count==stressSongs/3,"Large native library has exactly the Stable songs");
            Check(scanAttempts==0,"Zero ScanFiles calls while native display write lock is held");
            Check(refreshMilliseconds<5000,"20,000-song native refresh completes within five seconds");
            Check(ChartRecord.PathFieldNumber==7 && ChartRecord.ChartModeFieldNumber==15 && ChartRecord.RankStatusFieldNumber==16,"Actual protobuf field numbers match snapshot parser");
        }
        finally
        {
            blockScans=false; Plugin.SetSelection(StatusMask.All); rawClear.Invoke(raw,null);
            adapter.StatusStore.SetTestIndex(originalIndex);
            adapter.RefreshAfterIndex();
        }
    }

    private void TestControls()
    {
        captureCamera = new GameObject("VerificationCamera").AddComponent<Camera>();
        captureCamera.transform.position = new Vector3(0,0,-10); captureCamera.orthographic = true; captureCamera.orthographicSize = Height/2f;
        captureCamera.targetTexture = new RenderTexture(Width,Height,24);
        captureCamera.clearFlags = CameraClearFlags.SolidColor; captureCamera.backgroundColor = new Color(.04f,.05f,.07f,1);
        UnityEngine.Object.DontDestroyOnLoad(captureCamera.gameObject);
        var canvas = new GameObject("VerificationCanvas").AddComponent<Canvas>();
        searchCanvas=canvas;
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
        var sizes=toggles.Select(t=>t.GetComponent<RectTransform>().rect.size).ToArray();
        Check(sizes.All(s=>s==sizes[0]),"Three status controls have identical width and height");
        foreach(var toggle in toggles)
            Check(toggle.graphic.TryCast<Image>()?.sprite is not null,"Selected status uses an actual checkmark sprite");
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
        toggles[1].isOn=false;toggles[2].isOn=false;
        Check(toggles[0].graphic.canvasRenderer.GetAlpha()>.9f && toggles[1].graphic.canvasRenderer.GetAlpha()<.01f && toggles[2].graphic.canvasRenderer.GetAlpha()<.01f,"Only selected Stable has a visible checkmark");
        WriteResult();
    }
}
