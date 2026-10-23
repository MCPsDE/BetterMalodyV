using System.Reflection;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BetterMalodyV.Plugin;
using Malody.DB;
using Malody.Scene;
using UnityEngine;

namespace BetterMalodyV.Plugin;

internal static class RoomRuntimeTests
{
    internal static void Run(Action<bool,string> check)
    {
        var controller=RoomChartController.Current!;var api=controller.Api;
        var root=new GameObject("IsolatedRoomTests");root.SetActive(false);
        var room=root.AddComponent<SceneRoom>();
        var folder=Path.Combine(BepInEx.Paths.GameRootPath,"BetterMalodyV-test-share");Directory.CreateDirectory(folder);
        var target=Path.Combine(folder,"chart.mc");File.WriteAllText(target,"old local chart");
        var content=Encoding.UTF8.GetBytes("{\"meta\":{\"version\":\"host share\"}}");
        var hash=Convert.ToHexString(MD5.HashData(content)).ToLowerInvariant();
        var zipPath=Path.Combine(folder,"fixture.zip");
        using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create)){using var s=zip.CreateEntry("HostFolder/chart.mc").Open();s.Write(content);}
        object MakeSong(string chartHash,uint cid)
        {
            var type=Plugin.Adapter.SongType;var song=Activator.CreateInstance(type)!;
            var record=(SongRecord)type.GetProperties().Single(p=>p.PropertyType==typeof(SongRecord)).GetValue(song)!;
            record.Path=folder+"/";record.Searchwords=chartHash;record.Title="Fixture";
            type.GetProperties().Single(p=>p.PropertyType==typeof(bool) && p.Name.StartsWith("_") && p.Name.Contains("BackingField")).SetValue(song,true);
            var show=type.GetProperties().Single(p=>!p.CanWrite && p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().FullName=="Il2CppSystem.Collections.Generic.List`1");
            var chartType=show.PropertyType.GenericTypeArguments[0];var chart=Activator.CreateInstance(chartType)!;
            var meta=(ChartRecord)chartType.GetProperties().Single(p=>p.PropertyType==typeof(ChartRecord)).GetValue(chart)!;
            meta.Path=target;meta.Hash=chartHash;meta.Cid=cid;meta.Version="Lv.5";meta.NoteCount=100;meta.ChartMode=0x40;
            chartType.GetProperties().Single(p=>p.PropertyType==type && p.CanWrite).SetValue(chart,song);
            var list=show.GetValue(song)!;list.GetType().GetMethod("Add")!.Invoke(list,new[]{chart});
            var dictionaryProperty=type.GetProperties().Single(p=>p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().FullName=="Il2CppSystem.Collections.Generic.Dictionary`2");
            var dictionary=dictionaryProperty.GetValue(song)!;dictionary.GetType().GetMethod("Add")!.Invoke(dictionary,new object[]{"fixture",chart});
            return song;
        }
        NativePending Completed(Type taskType,object value)=>new(Activator.CreateInstance(taskType,new[]{value})!);
        void Pump(IEnumerator<object?> routine)
        {
            using(routine)
                while(routine.MoveNext())
                {
                    if(routine.Current is NativePending native)
                    {
                        if(!native.Poll())throw new Exception("Fixture native task should be complete");
                    }
                    else if(routine.Current is Task task) task.GetAwaiter().GetResult();
                }
        }
        var info=new RoomInfo {Roomid=12,Hostid=200,ChartId=123,SongId=456,ChartHash=new string('a',32),ChartTitle="Fixture"};
        var selfType=typeof(Malody.Chart.PlayModeType).Assembly.GetTypes().Single(t=>t.GetMethods().Any(m=>m.Name=="get_Self"));
        var self=selfType.GetMethod("get_Self")!.Invoke(RoomGameApi.Singleton(selfType),null)!;
        var user=self.GetType().GetProperties().Single(p=>p.PropertyType==typeof(uint));var originalUser=user.GetValue(self);
        try
        {
            user.SetValue(self,(uint)200);
            var oldSong=MakeSong(info.ChartHash,info.ChartId);var oldChart=api.Chart(oldSong,info.ChartHash)!;
            var newSong=MakeSong(hash,info.ChartId);var newChart=api.Chart(newSong,hash)!;
            api.SelectedTest=()=>oldChart;
            var responseType=api.QueryTaskType.GenericTypeArguments[0];var pack=Activator.CreateInstance(responseType)!;
            var itemsProperty=responseType.GetProperty("Items")!;
            var itemType=itemsProperty.PropertyType.GenericTypeArguments[0];var item=Activator.CreateInstance(itemType)!;
            RoomGameApi.Set(item,"Name","chart.mc");RoomGameApi.Set(item,"Hash",hash);
            var array=Activator.CreateInstance(itemsProperty.PropertyType,new object[]{(long)1})!;
            array.GetType().GetProperty("Item")!.SetValue(array,item,new object[]{0});itemsProperty.SetValue(pack,array);
            api.QueryTest=_=>Completed(api.QueryTaskType,pack);
            controller.TestContext(room,info);Pump(controller.TestCheck(room,new RoomInfo(info)));
            check(controller.TestNeedsUpdate,"Host stale chart exposes update action after official manifest comparison");
            var selected=false;api.DownloadTest=_=>Completed(api.DownloadTaskType,newSong);api.SelectTest=(_,c)=>selected=api.Hash(c)==hash;
            Pump(controller.TestDownload(room,new RoomInfo(info)));
            check(selected,"Host download selects verified updated chart through room selection flow");
            controller.TestContext(room,info);Pump(controller.TestCheck(room,new RoomInfo(info)));
            api.DownloadTest=_=>Completed(api.DownloadTaskType,oldSong);selected=false;
            try{Pump(controller.TestDownload(room,new RoomInfo(info)));throw new Exception("Accepted stale official result");}
            catch(IOException){check(!selected,"Stale official download never selects a mismatched chart");}
            info.ChartHash=hash;info.Hostid=999;controller.TestContext(room,info);
            api.ExistingTest=_=>folder;api.ReloadTest=_=>newSong;var notified=false;api.RefreshTest=(_,_)=>notified=true;
            RoomChartController.DownloadFileTest=(_,path)=>{File.Copy(zipPath,path,true);return Task.CompletedTask;};
            var share=new SrvHostShare {Charthash=hash,DownloadUrl="https://fixture.invalid/share.zip",Session=1};
            Pump(controller.TestReceive(room,new RoomInfo(info),share));
            check(File.ReadAllBytes(target).SequenceEqual(content) && notified,"Member replaces stale local file and refreshes valid room chart");
            File.WriteAllText(target,"old local chart");notified=false;api.ReloadTest=_=>oldSong;
            try{Pump(controller.TestReceive(room,new RoomInfo(info),share));throw new Exception("Accepted mismatched native reload");}
            catch(IOException){check(File.ReadAllText(target)=="old local chart" && !notified,"Native reload hash mismatch rolls local files back");}
            api.ReloadTest=_=>newSong;
            RoomChartController.DownloadFileTest=(_,path)=>{File.Copy(zipPath,path,true);api.SetTestInfo(room,new RoomInfo(info){ChartHash=new string('b',32)});return Task.CompletedTask;};
            Pump(controller.TestReceive(room,new RoomInfo(info),share));
            check(File.ReadAllText(target)=="old local chart","Changing room chart during download cancels overwrite");
        }
        finally
        {
            user.SetValue(self,originalUser);api.ResetTests();RoomChartController.DownloadFileTest=null;
            root.SetActive(false);UnityEngine.Object.Destroy(root);Directory.Delete(folder,true);
        }
    }
}
