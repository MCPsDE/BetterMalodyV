using System.Reflection;
using System.Collections;
using System.Net.Http;
using HarmonyLib;
using BetterMalodyV.Shared;
using Malody.DB;
using Malody.Scene;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace BetterMalodyV.Plugin;

public sealed class RoomChartController : MonoBehaviour
{
    public RoomChartController(IntPtr pointer):base(pointer) { }
    internal static RoomChartController? Current;
    internal RoomGameApi Api=null!;
    private SceneRoom? room;
    private Button? update;
    private Malody.Scene.MP.PanelRoomSong? boundPanel;
    private Vector2 titleRight,artistRight;
    private IEnumerator<object?>? job;
    private object? pending;
    private string key="";
    private string checkedKey="";
    private string latestHash="";
    private float nextScan;
    private float nextQuery;
    private bool busy,needsUpdate;
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal void Initialize(Harmony harmony)
    {
        Api=new RoomGameApi();Current=this;
        harmony.Patch(Api.ShareMoveNext,prefix:new HarmonyMethod(typeof(RoomChartController),nameof(SharedBefore)));
        Plugin.Current.LogRoom("Room chart update/share bindings installed.");
    }
    public void Update()
    {
        if(Api is null)return;
        try
        {
            Advance();
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.3f;
            var active=UnityEngine.Object.FindObjectOfType<SceneRoom>();
            if(!active || !active.gameObject.activeInHierarchy){room=null;key="";return;}
            room=active;var info=Api.Info(active);
            if(info is null || !active.panelSong)return;
            var next=info.Roomid+":"+info.Hostid+":"+info.ChartHash;
            if(next!=key) { key=next;needsUpdate=false;latestHash="";checkedKey="";nextQuery=0; }
            if(!update || boundPanel!=active.panelSong) CreateButton(active);
            ShowAction(busy || Api.IsHost(info) && needsUpdate);
            update!.interactable=!busy;
            if(job is null && !busy && (checkedKey!=key || Time.unscaledTime>=nextQuery) && Api.IsHost(info) && info.Status==0 && info.ChartId>0 && info.ChartHash.Length==32)
            {
                checkedKey=key;nextQuery=Time.unscaledTime+60;
                Start(CheckHost(active,new RoomInfo(info),key));
            }
        }
        catch(Exception ex) { Fail(ex); }
    }
    private void CreateButton(SceneRoom owner)
    {
        boundPanel=owner.panelSong;
        var anchor=owner.panelSong.iconDL.GetComponent<RectTransform>();
        update=NativeUi.Action(anchor.parent,owner.panelSong.artist,"下载更新");
        titleRight=owner.panelSong.title.rectTransform.offsetMax;artistRight=owner.panelSong.artist.rectTransform.offsetMax;
        var text=update.GetComponentInChildren<TextMeshProUGUI>();
        if(!text.font.HasCharacter('下')) {text.text="Update";update.GetComponent<RectTransform>().sizeDelta=new Vector2(text.GetPreferredValues("Update").x+32,44);}
        var rect=update.GetComponent<RectTransform>();
        rect.anchorMin=anchor.anchorMin;rect.anchorMax=anchor.anchorMax;rect.pivot=new Vector2(1,.5f);
        rect.anchoredPosition=anchor.anchoredPosition-new Vector2(anchor.rect.width*anchor.pivot.x+12,0);
        update.onClick.AddListener((UnityAction)(()=>
        {
            if(busy||!room)return;var info=Api.Info(room!);
            if(!Api.IsHost(info)||!needsUpdate)return;
            Start(DownloadHost(room!,new RoomInfo(info),key));
        }));
        update.gameObject.SetActive(false);
    }
    private void ShowAction(bool visible)
    {
        if(!update || !boundPanel)return;
        update!.gameObject.SetActive(visible);
        var actionRect=update.GetComponent<RectTransform>();
        var inset=actionRect.rect.width-actionRect.anchoredPosition.x+12;
        boundPanel!.title.rectTransform.offsetMax=visible?new Vector2(Math.Min(titleRight.x,-inset),titleRight.y):titleRight;
        boundPanel.artist.rectTransform.offsetMax=visible?new Vector2(Math.Min(artistRight.x,-inset),artistRight.y):artistRight;
    }
    private bool Still(SceneRoom owner,RoomInfo info,string expected)
    {
        if(!owner || room!=owner || key!=expected)return false;
        var current=Api.Info(owner);return current.Roomid==info.Roomid && current.Hostid==info.Hostid && current.ChartHash==info.ChartHash && current.Status==0;
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private IEnumerator<object?> CheckHost(SceneRoom owner,RoomInfo info,string expected)
    {
        var request=Api.Query(info);yield return request;
        if(!Still(owner,info,expected))yield break;
        var response=request.Result;
        if(response is null)yield break;
        var items=RoomGameApi.Get(response,"Items");
        var length=Convert.ToInt32(RoomGameApi.Get(items,"Length"));
        var hashes=new List<string>();
        var item=items.GetType().GetProperty("Item")!;
        for(var i=0;i<length;i++)
        {
            var value=item.GetValue(items,new object[]{i})!;
            var name=(string)RoomGameApi.Get(value,"Name");
            if(name.EndsWith(".mc",StringComparison.OrdinalIgnoreCase))hashes.Add((string)RoomGameApi.Get(value,"Hash"));
        }
        var local=Api.Selected;
        if(local is not null && Api.Hash(local)==info.ChartHash && hashes.Count==1 && hashes[0].Length==32)
        { latestHash=hashes[0];needsUpdate=!latestHash.Equals(info.ChartHash,StringComparison.OrdinalIgnoreCase); }
        if(needsUpdate && update)ShowAction(true);
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private IEnumerator<object?> DownloadHost(SceneRoom owner,RoomInfo info,string expected)
    {
        busy=true;SetLabel("下载中…","Downloading…");
        try
        {
            var request=Api.Download(info);yield return request;
            if(!Still(owner,info,expected) || !Api.IsHost(Api.Info(owner)))yield break;
            var song=request.Result??throw new IOException("Official chart download/import failed.");
            var chart=Api.Chart(song,latestHash)??Api.ChartByCid(song,info.ChartId)??throw new IOException("Downloaded chart ID is missing or ambiguous.");
            if(Api.Hash(chart)!=latestHash)throw new IOException("Downloaded chart hash differs from the current official version.");
            Api.Meta(chart).Cid=info.ChartId;
            Api.SaveChart(chart);
            Api.SelectHost(owner,chart);needsUpdate=false;
            Plugin.Current.LogRoom("Room host selected the updated official chart.");
        }
        finally { busy=false;SetLabel("下载更新","Update"); }
    }
    private void SetLabel(string chinese,string english)
    {
        if(!update)return;var text=update!.GetComponentInChildren<TextMeshProUGUI>();
        text.text=text.font.HasCharacter('下')?chinese:english;
        update.GetComponent<RectTransform>().sizeDelta=new Vector2(text.GetPreferredValues(text.text).x+32,44);
        ShowAction(update.gameObject.activeSelf);
    }
    private static bool SharedBefore(object __instance)
    {
        var controller=Current;if(!controller || controller!.Api is null)return true;
        try
        {
            var state=__instance.GetType().GetProperty("__1__state")!;
            if(Convert.ToInt32(state.GetValue(__instance))!=-1)return true;
            var data=(SrvHostShare)RoomGameApi.Get(__instance,"data");
            var owner=UnityEngine.Object.FindObjectOfType<SceneRoom>();
            if(!owner)return true;
            var info=controller.Api.Info(owner);
            if(controller.Api.IsHost(info) || info.Status!=0 || data.Charthash!=info.ChartHash || data.Charthash.Length!=32)return true;
            if(!Uri.TryCreate(data.DownloadUrl,UriKind.Absolute,out var url) || url.Scheme!="https")return true;
            controller.room=owner;controller.key=info.Roomid+":"+info.Hostid+":"+info.ChartHash;
            if(controller.busy)return false;
            controller.job?.Dispose();controller.job=null;controller.pending=null;
            var local=controller.Api.Selected;
            if(local is not null && controller.Api.Hash(local)==data.Charthash)return false;
            state.SetValue(__instance,-2);
            controller.Start(controller.Receive(owner,new RoomInfo(info),new SrvHostShare(data),controller.key));
            return false;
        }
        catch(Exception ex) { controller.Fail(ex);return true; }
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private IEnumerator<object?> Receive(SceneRoom owner,RoomInfo info,SrvHostShare share,string expected)
    {
        busy=true;SetLabel("接收中…","Receiving…");SharedChartInstall? operation=null;var committed=false;
        var path=Path.Combine(Path.GetTempPath(),"BetterMalodyV-share-download-"+Guid.NewGuid().ToString("N")+".zip");
        try
        {
            var download=DownloadFile(share.DownloadUrl,path);yield return download;
            if(!Still(owner,info,expected))yield break;
            var chartRoot=Api.ChartRoot;var existing=Api.ExistingSong(info.SongId);
            var replaced=Api.ExistingChartPath(existing,info.ChartId);
            var prepare=Task.Run(()=>SharedChartInstall.Prepare(path,chartRoot,share.Charthash,existing,replaced));yield return prepare;
            operation=prepare.Result;
            if(!Still(owner,info,expected))yield break;
            var song=Api.Reload(operation.SongPath);
            var chart=Api.Chart(song,share.Charthash)??throw new IOException("Imported share did not produce the host's hash.");
            operation.Commit();committed=true;Api.Refresh(owner,info);
            Plugin.Current.LogRoom("Replaced mismatched local room chart with verified host share.");
        }
        finally
        {
            operation?.Dispose();
            if(operation is not null && !committed) Api.Reload(operation.SongPath);
            busy=false;SetLabel("下载更新","Update");
            try {File.Delete(path);}catch(IOException) { }
        }
    }
    private static async Task DownloadFile(string url,string destination)
    {
#if BETTER_MALODYV_VERIFY
        if(DownloadFileTest is not null){await DownloadFileTest(url,destination);return;}
#endif
        using var client=new HttpClient {Timeout=TimeSpan.FromMinutes(5)};
        using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>1024L*1024*1024)throw new IOException("Shared archive is too large.");
        await using var input=await response.Content.ReadAsStreamAsync();await using var output=File.Create(destination);
        var buffer=new byte[128*1024];long total=0;int count;
        while((count=await input.ReadAsync(buffer))>0) {total+=count;if(total>1024L*1024*1024)throw new IOException("Shared archive is too large.");await output.WriteAsync(buffer.AsMemory(0,count));}
    }
#if BETTER_MALODYV_VERIFY
    internal static Func<string,string,Task>? DownloadFileTest;
    internal void TestContext(SceneRoom value,RoomInfo info)
    {
        room=value;Api.SetTestInfo(value,info);key=info.Roomid+":"+info.Hostid+":"+info.ChartHash;
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal IEnumerator<object?> TestCheck(SceneRoom value,RoomInfo info)=>CheckHost(value,info,key);
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal IEnumerator<object?> TestDownload(SceneRoom value,RoomInfo info)=>DownloadHost(value,info,key);
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal IEnumerator<object?> TestReceive(SceneRoom value,RoomInfo info,SrvHostShare share)=>Receive(value,info,share,key);
    internal bool TestNeedsUpdate=>needsUpdate;
    internal Button TestAction(SceneRoom owner){CreateButton(owner);ShowAction(true);return update!;}
#endif
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void Start(IEnumerator<object?> routine)
    {
        if(job is not null)return;job=routine;pending=null;
    }
    private void Advance()
    {
        if(job is null)return;
        if(pending is NativePending native && !native.Poll())return;
        if(pending is Task task)
        {
            if(!task.IsCompleted)return;task.GetAwaiter().GetResult();
        }
        pending=null;
        if(job.MoveNext())pending=job.Current;
        else {job.Dispose();job=null;}
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void Fail(Exception ex)
    {
        try {job?.Dispose();}catch(Exception restore){Plugin.Current.LogRoom("Room rollback failed: "+restore.Message);}
        job=null;pending=null;busy=false;checkedKey=key;
        SetLabel("重试更新","Retry update");Plugin.Current.LogRoom("Room chart operation failed: "+ex.GetBaseException().Message);
        try{if(room)Api.ShowFailure();}catch(Exception message){Plugin.Current.LogRoom("Room error notification failed: "+message.Message);}
    }
}
