using BetterMalodyV.Shared;
using Malody.Manager;
using Malody.Scene.Panel.Dialog;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterMalodyV.Plugin;

// Keep the game's real dialog, keyboard shortcuts, blur and close behavior.
internal sealed class GameUpdateDialog
{
    private PanelDialog? dialog;
    private RectTransform? fill;
    private GameUpdateStage stage;
    private string version="";
    private bool shown;
    internal bool ReadyPresented { get; private set; }
    internal void Render(GameUpdateProgress? value,bool canShow,float restartSeconds=0)
    {
        if(value is null || !value.Visible) {Close();shown=false;version="";return;}
        if(version!=value.Version || stage!=value.Stage && value.Stage is GameUpdateStage.Ready or GameUpdateStage.Failed)shown=false;
        version=value.Version;stage=value.Stage;
        if(stage!=GameUpdateStage.Ready)ReadyPresented=false;
        if(!canShow){Close();shown=false;return;}
        if(!dialog && !shown)Create();
        if(!dialog)return;
        shown=true;
        var chinese=dialog!.desc.font.HasCharacter('更',true,true);
        var heading=chinese?"插件更新":"Plugin update";
        typeof(PanelDialog).GetMethod("set_TextTitle")!.Invoke(dialog,new object[]{heading});
        dialog.textOK.text=chinese?"隐藏":"Hide";
        dialog.desc.text=Describe(value,chinese,restartSeconds);
        if(stage==GameUpdateStage.Ready && IsVisible())ReadyPresented=true;
        if(fill)
        {
            var fraction=value.Stage==GameUpdateStage.Ready?1:value.Stage==GameUpdateStage.Downloading?value.Fraction:null;
            if(fraction.HasValue){fill!.anchorMin=Vector2.zero;fill.anchorMax=new Vector2((float)fraction.Value,1);}
            else {var x=Mathf.PingPong(Time.unscaledTime*.4f,.75f);fill!.anchorMin=new Vector2(x,0);fill.anchorMax=new Vector2(x+.25f,1);}
            fill!.gameObject.SetActive(value.Stage!=GameUpdateStage.Failed);
        }
    }
    private void Create()
    {
        var front=UnityEngine.Object.FindObjectOfType<FrontLayerManager>();if(!front)return;
        var root=front.Instantiate(FrontLayerManager.PrefabType.Dialog,false);if(!root)return;
        try
        {
            var candidate=root.GetComponent<PanelDialog>();
#if BETTER_MALODYV_VERIFY
            TestCreateFailure?.Invoke();
#endif
            candidate.allowAbort=true;typeof(PanelDialog).GetMethod("Show")!.Invoke(candidate,new object[]{""});
            typeof(PanelDialog).GetMethod("set_HideCancel")!.Invoke(candidate,new object[]{true});
            candidate.textMiddle.transform.parent.gameObject.SetActive(false);
            candidate.desc.richText=false;
            var track=Rect("BetterMalodyV.DownloadProgress",candidate.desc.transform);
            track.anchorMin=new Vector2(0,0);track.anchorMax=new Vector2(1,0);
            track.offsetMin=new Vector2(0,-26);track.offsetMax=new Vector2(0,-18);
            var background=track.gameObject.AddComponent<Image>();background.color=new Color(.8f,.8f,.8f,.15f);background.raycastTarget=false;
            var candidateFill=Rect("Fill",track);candidateFill.anchorMin=Vector2.zero;candidateFill.anchorMax=Vector2.one;candidateFill.offsetMin=candidateFill.offsetMax=Vector2.zero;
            var image=candidateFill.gameObject.AddComponent<Image>();image.color=candidate.desc.color;image.raycastTarget=false;
            dialog=candidate;fill=candidateFill;
        }
        catch {front.LayerClose(root);UnityEngine.Object.Destroy(root);ReadyPresented=false;throw;}
    }
    private bool IsVisible()
    {
        if(!dialog || !dialog!.gameObject.activeInHierarchy)return false;
        var canvas=dialog.GetComponentInParent<Canvas>();if(!canvas || !canvas.isActiveAndEnabled)return false;
        foreach(var group in dialog.GetComponentsInParent<CanvasGroup>())if(group.alpha<=.01f || !group.blocksRaycasts)return false;
        return true;
    }
    private static RectTransform Rect(string name,Transform parent)
    {
        var go=new GameObject(name);go.layer=parent.gameObject.layer;
        var rect=go.AddComponent<RectTransform>();rect.SetParent(parent,false);return rect;
    }
    internal void Close()
    {
        if(dialog)dialog!.OnCloseDirect(false);
        dialog=null;fill=null;
        ReadyPresented=false;
    }
    private static string Describe(GameUpdateProgress value,bool chinese,float restartSeconds)
    {
        var title="BetterMalodyV "+value.Version;
        var amount=$"{value.DownloadedBytes/1048576d:F1} MB";
        if(value.TotalBytes is >0)amount=$"{value.DownloadedBytes/1048576d:F1} / {value.TotalBytes.Value/1048576d:F1} MB · {value.Fraction:P0}";
        return value.Stage switch
        {
            GameUpdateStage.Downloading=>chinese?$"{title}\n正在下载更新\n{amount}\n校验后将自动安装并重启游戏。":$"{title}\nDownloading update\n{amount}\nThe game will restart after verification.",
            GameUpdateStage.Verifying=>chinese?$"{title}\n正在校验安装包…\n校验通过后将自动安装并重启游戏。":$"{title}\nVerifying download…\nThe game will restart after verification.",
            GameUpdateStage.Ready=>chinese?$"{title}\n更新已就绪\n{(restartSeconds>0?$"{Math.Ceiling(restartSeconds)} 秒后退出游戏安装并重启。":"等待当前游玩或编辑结束后安装并重启。")}":$"{title}\nUpdate ready\n{(restartSeconds>0?$"Restarting to install in {Math.Ceiling(restartSeconds)} seconds.":"Waiting for play or editing to finish.")}",
            _=>chinese?$"{title}\n更新未完成\n你可以继续游戏，稍后会自动重试。":$"{title}\nUpdate could not finish\nYou can keep playing. We will retry later."
        };
    }
#if BETTER_MALODYV_VERIFY
    internal static Action? TestCreateFailure;
    internal PanelDialog? TestPanel=>dialog;
    internal RectTransform? TestFill=>fill;
#endif
}
