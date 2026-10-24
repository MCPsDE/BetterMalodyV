using System.Reflection;
using Malody.DB;
using Malody.Scene;
using Malody.Scene.MP;
using Malody.Chart;
using Il2CppInterop.Runtime.InteropTypes;
using Malody.Network;
using Malody.Network.Telepathy;

namespace BetterMalodyV.Plugin;

/// <summary>Bindings for the supported 6.7 release. Private aliases are isolated
/// here and validated by type/signature before room features are enabled.</summary>
internal sealed class RoomGameApi
{
    private const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    internal Type OnlineType { get; }
    internal Type RecordType { get; }
    internal MethodInfo ShareReceive { get; }
    internal MethodInfo ShareEvent { get; }
    private readonly Type playType,infoType;
    private readonly MethodInfo query,download,sendChange,reload;
    private readonly PropertyInfo roomInfo,chartMeta,songMeta,showList,chartSong,shareResponse;
    internal RoomGameApi()
    {
        var framework=typeof(PlayModeType).Assembly;var game=typeof(SceneRoom).Assembly;
        OnlineType=framework.GetTypes().Single(t=>t.GetMethods(Any).Any(m=>m.Name=="DownloadAndImport"));
        download=OnlineType.GetMethods(Any).Single(m=>m.Name=="DownloadAndImport" && m.GetParameters().Length==1);
        RecordType=download.GetParameters()[0].ParameterType;
        query=OnlineType.GetMethods(Any).Single(m=>m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType==RecordType && m.ReturnType.IsGenericType && m.ReturnType.GenericTypeArguments[0].FullName=="Malody.JSON.RespDownload");
        RequireRecord("yug",typeof(uint));RequireRecord("yuh",typeof(uint));RequireRecord("yuj",typeof(string));RequireRecord("yuk",typeof(string));
        playType=game.GetTypes().Single(t=>t.GetMethods(Any).Any(m=>m.Name=="get_Local" && m.ReturnType==t));
        infoType=framework.GetTypes().Single(t=>t.Namespace=="Malody.Network" && t.GetMethods(Any).Any(m=>m.Name=="get_Self"));
        roomInfo=typeof(SceneRoom).GetProperties(Any).Single(p=>p.PropertyType==typeof(RoomInfo));
        reload=Plugin.Adapter.ManagerType.GetMethod("ReloadSong")!;
        showList=Plugin.Adapter.SongType.GetProperties(Any).Single(p=>!p.CanWrite && p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().FullName=="Il2CppSystem.Collections.Generic.List`1");
        var chartType=showList.PropertyType.GenericTypeArguments[0];
        sendChange=typeof(SceneRoom).GetMethods(Any).Single(m=>m.GetParameters() is var p && p.Length==2 && p[0].ParameterType==chartType && p[1].ParameterType==typeof(bool));
        chartMeta=chartType.GetProperties(Any).Single(p=>p.PropertyType==typeof(ChartRecord));
        chartSong=chartType.GetProperties(Any).Single(p=>p.PropertyType==Plugin.Adapter.SongType && p.CanWrite);
        songMeta=Plugin.Adapter.SongType.GetProperties(Any).Single(p=>p.PropertyType==typeof(SongRecord));
        // Supported 6.7 aliases. The TCP event is a delegate boundary; native
        // builds can inline calls to HandleHostShare/HandleChartShare inside it.
        ShareEvent=typeof(WidgetMPShare).GetMethod("rwy",Any,null,new[]{typeof(TCPResponseTag),typeof(ipa)},null)
            ?? throw new NotSupportedException("Room share notification entry point changed.");
        ShareReceive=typeof(WidgetMPShare).GetMethod("rxb",Any,null,new[]{typeof(SrvHostShare)},null)
            ?? throw new NotSupportedException("Room chart-share entry point changed.");
        shareResponse=typeof(ipa).GetProperty("zab",Any)
            ?? throw new NotSupportedException("Room share response field changed.");
        if(ShareReceive.ReturnType!=typeof(void)||ShareReceive.IsStatic||ShareEvent.ReturnType!=typeof(void)||ShareEvent.IsStatic
            ||shareResponse.PropertyType!=typeof(Google.Protobuf.IMessage))
            throw new NotSupportedException("Room chart-share entry point signature changed.");
    }
    internal SrvHostShare? ReadShare(ipa request)=>(shareResponse.GetValue(request) as Il2CppObjectBase)?.TryCast<SrvHostShare>();
    private void RequireRecord(string name,Type type) { if(RecordType.GetProperty(name)?.PropertyType!=type)throw new NotSupportedException("Room download binding changed: "+name); }
    internal static object Get(object value,string name)=>value.GetType().GetProperty(name,Any)!.GetValue(value)!;
    internal static void Set(object value,string name,object? data)=>value.GetType().GetProperty(name,Any)!.SetValue(value,data);
    internal static object Singleton(Type type)=>type.GetMethods(Any|BindingFlags.FlattenHierarchy).Single(m=>m.Name=="get_G" && m.GetParameters().Length==0).Invoke(null,null)!;
    private object Local=>playType.GetMethod("get_Local",Any)!.Invoke(null,null)!;
    internal object? Selected
    {
        get
        {
#if BETTER_MALODYV_VERIFY
            if(SelectedTest is not null)return SelectedTest();
#endif
            return playType.GetMethod("get_Chart",Any)!.Invoke(Local,null);
        }
    }
    internal RoomInfo Info(SceneRoom room)=>(RoomInfo)roomInfo.GetValue(room)!;
    internal bool IsHost(RoomInfo info)
    {
        var self=infoType.GetMethod("get_Self",Any)!.Invoke(Singleton(infoType),null)!;
        var userId=self.GetType().GetProperties(Any).Single(p=>p.PropertyType==typeof(uint));
        return info.Hostid!=0 && info.Hostid==Convert.ToUInt32(userId.GetValue(self));
    }
    internal object Record(RoomInfo info,string? hash=null)
    {
        var record=Activator.CreateInstance(RecordType)!;
        Set(record,"yug",info.ChartId);Set(record,"yuh",info.SongId);Set(record,"yuj",hash??"");Set(record,"yuk",info.ChartTitle);Set(record,"version",info.ChartDiff);
        return record;
    }
    internal NativePending Query(RoomInfo info)
    {
#if BETTER_MALODYV_VERIFY
        if(QueryTest is not null)return QueryTest(info);
#endif
        return new(query.Invoke(Singleton(OnlineType),new[] { Record(info) })!);
    }
    internal NativePending Download(RoomInfo info)
    {
#if BETTER_MALODYV_VERIFY
        if(DownloadTest is not null)return DownloadTest(info);
#endif
        return new(download.Invoke(Singleton(OnlineType),new[] { Record(info) })!);
    }
    internal object? Chart(object song,string hash)=>Plugin.Adapter.SongType.GetMethod("GetChartByHash")!.Invoke(song,new object[]{hash});
    internal object? ChartByCid(object song,uint cid)
    {
        var list=showList.GetValue(song)!;var count=(int)Get(list,"Count");var item=list.GetType().GetProperty("Item")!;
        var matches=new List<object>();for(var i=0;i<count;i++){var chart=item.GetValue(list,new object[]{i})!;if(Meta(chart).Cid==cid)matches.Add(chart);}
        return matches.Count==1?matches[0]:null;
    }
    internal ChartRecord Meta(object chart)=>(ChartRecord)chartMeta.GetValue(chart)!;
    internal string Hash(object chart)=>Meta(chart).Hash;
    internal bool HasChart(string hash)
    {
#if BETTER_MALODYV_VERIFY
        if(HasChartTest is not null)return HasChartTest(hash);
#endif
        return Plugin.Adapter.ManagerType.GetMethod("GetChartByHash")!
            .Invoke(Singleton(Plugin.Adapter.ManagerType),new object[]{hash}) is not null;
    }
    internal object Song(object chart)=>chartSong.GetValue(chart)!;
    internal object Reload(string path)
    {
#if BETTER_MALODYV_VERIFY
        if(ReloadTest is not null)return ReloadTest(path);
#endif
        return reload.Invoke(Singleton(Plugin.Adapter.ManagerType),new object[]{path,true})!;
    }
    internal void SelectHost(SceneRoom room,object chart)
    {
#if BETTER_MALODYV_VERIFY
        if(SelectTest is not null){SelectTest(room,chart);return;}
#endif
        sendChange.Invoke(room,new object[]{chart,true});
    }
    internal void SaveChart(object chart)
    {
#if BETTER_MALODYV_VERIFY
        if(SelectTest is not null)return;
#endif
        chart.GetType().GetMethod("SaveMetaDB")!.Invoke(chart,null);
    }
    internal void Refresh(SceneRoom room,RoomInfo info)
    {
#if BETTER_MALODYV_VERIFY
        if(RefreshTest is not null){RefreshTest(room,info);return;}
#endif
        room.panelSong.Fill(info);
        var type=typeof(PlayModeType).Assembly.GetTypes().Single(t=>t.GetMethods(Any).Any(m=>m.Name=="Notify"));
        var notify=type.GetMethods(Any).Single(m=>m.Name=="Notify" && m.GetParameters().Length==2);
        var kind=Enum.Parse(notify.GetParameters()[0].ParameterType,"ChangeSong");
        var chart=Selected;
        notify.Invoke(null,new object?[]{kind,chart is null?null:Song(chart)});
    }
    internal string ChartRoot=>Path.Combine(BepInEx.Paths.GameRootPath,"chart");
    internal void ShowFailure()
    {
        var type=typeof(SceneRoom).Assembly.GetTypes().Single(t=>t.Namespace=="Malody.Manager" && t.GetMethods(Any).Any(m=>m.Name=="ShowMessage" && m.GetParameters().Length==4));
        var method=type.GetMethods(Any).Single(m=>m.Name=="ShowMessage" && m.GetParameters().Length==4 && m.GetParameters()[0].ParameterType==typeof(string));
        var parameters=method.GetParameters();
        method.Invoke(Singleton(type),new[]{(object)"Chart update failed. Please retry.",Enum.Parse(parameters[1].ParameterType,"Error"),Enum.Parse(parameters[2].ParameterType,"Short"),(object)0});
    }
    internal string? ExistingSong(uint sid)
    {
#if BETTER_MALODYV_VERIFY
        if(ExistingTest is not null)return ExistingTest(sid);
#endif
        if(sid==0)return null;
        var manager=Singleton(Plugin.Adapter.ManagerType);
        var raw=Plugin.Adapter.ManagerType.GetProperties(Any).Single(p=>p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().FullName=="Il2CppSystem.Collections.Generic.Dictionary`2");
        var dictionary=raw.GetValue(manager)!;var values=Get(dictionary,"Values");var enumerator=values.GetType().GetMethod("GetEnumerator")!.Invoke(values,null)!;
        var next=enumerator.GetType().GetMethod("MoveNext")!;var current=enumerator.GetType().GetProperty("Current")!;var paths=new List<string>();
        while((bool)next.Invoke(enumerator,null)!) {var song=current.GetValue(enumerator)!;var meta=(SongRecord)songMeta.GetValue(song)!;if(meta.Sid==sid)paths.Add(meta.Path);}
        return paths.Count==1?paths[0]:null;
    }
    internal string? ExistingChartPath(string? songPath,uint cid)
    {
        if(songPath is null || cid==0)return null;
#if BETTER_MALODYV_VERIFY
        if(ReloadTest is not null)return null;
#endif
        var manager=Singleton(Plugin.Adapter.ManagerType);
        var raw=Plugin.Adapter.ManagerType.GetProperties(Any).Single(p=>p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().FullName=="Il2CppSystem.Collections.Generic.Dictionary`2");
        var values=Get(raw.GetValue(manager)!,"Values");var enumerator=values.GetType().GetMethod("GetEnumerator")!.Invoke(values,null)!;
        var next=enumerator.GetType().GetMethod("MoveNext")!;var current=enumerator.GetType().GetProperty("Current")!;
        while((bool)next.Invoke(enumerator,null)!)
        {
            var song=current.GetValue(enumerator)!;var meta=(SongRecord)songMeta.GetValue(song)!;
            if(!string.Equals(Path.GetFullPath(meta.Path).TrimEnd('\\'),Path.GetFullPath(songPath).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))continue;
            Plugin.Adapter.ScanFiles.Invoke(song,new object[]{false});
            var chart=ChartByCid(song,cid);return chart is null?null:Meta(chart).Path;
        }
        return null;
    }
#if BETTER_MALODYV_VERIFY
    internal Func<RoomInfo,NativePending>? QueryTest,DownloadTest;
    internal Func<string,object>? ReloadTest;
    internal Action<SceneRoom,object>? SelectTest;
    internal Action<SceneRoom,RoomInfo>? RefreshTest;
    internal Func<uint,string?>? ExistingTest;
    internal Func<object?>? SelectedTest;
    internal Func<string,bool>? HasChartTest;
    internal Type QueryTaskType=>query.ReturnType;
    internal Type DownloadTaskType=>download.ReturnType;
    internal void SetTestInfo(SceneRoom room,RoomInfo info)=>roomInfo.SetValue(room,info);
    internal void SetTestSelected(object? chart)=>playType.GetMethod("set_Chart",Any)!.Invoke(Local,new[]{chart});
    internal void TestShareNotification(WidgetMPShare widget,SrvHostShare share)
    {
        var request=new ipa();shareResponse.SetValue(request,share.Cast<Google.Protobuf.IMessage>());
        ShareEvent.Invoke(widget,new object[]{TCPResponseTag.SrvRoomHostShare,request});
    }
    internal void ResetTests(){QueryTest=null;DownloadTest=null;ReloadTest=null;SelectTest=null;RefreshTest=null;ExistingTest=null;SelectedTest=null;HasChartTest=null;}
#endif
}
