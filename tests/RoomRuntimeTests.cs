using System.Reflection;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BetterMalodyV.Plugin;
using Malody.DB;
using Malody.Scene;
using Malody.Scene.MP;
using UnityEngine;
using HarmonyLib;

namespace BetterMalodyV.Plugin;

internal static class RoomRuntimeTests
{
    private static int nativeDownloads;
    private static bool BlockNativeDownload(){nativeDownloads++;return false;}
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

            // Exercise the actual patched native share entry, rather than only
            // calling Receive directly (which cannot catch enqueue/reentry bugs).
            var widget=root.AddComponent<WidgetMPShare>();room.mpShare=widget;widget.roomId=info.Roomid;
            var guard=new Harmony("com.mcpsde.bettermalodyv.verify.share-enqueue");
            nativeDownloads=0;
            foreach(var method in api.OnlineType.GetMethods().Where(method=>method.Name=="DownloadAndImport"))
                guard.Patch(method,prefix:new HarmonyMethod(typeof(RoomRuntimeTests),nameof(BlockNativeDownload)));
            try
            {
                // Feed an actual native RequestItem to the subscribed TCP event,
                // including its native IMessage-to-SrvHostShare cast.
                void Deliver(SrvHostShare message)=>api.TestShareNotification(widget,message);
                void Drain()
                {
                    for(var step=0;controller.TestHasJob&&step<20;step++)
                    {
                        controller.TestAdvance();
                        if(controller.TestPending is Task task)task.GetAwaiter().GetResult();
                    }
                    if(controller.TestHasJob)throw new Exception("Share job did not finish within the bounded fixture steps");
                }
                controller.TestContext(room,info);controller.TestResetShares();api.SelectedTest=()=>oldChart;
                api.HasChartTest=_=>false;api.ReloadTest=_=>newSong;
                var downloadCount=0;var refreshCount=0;
                var releaseDownload=new TaskCompletionSource<bool>();
                RoomChartController.DownloadFileTest=(_,path)=>
                {
                    downloadCount++;File.Copy(zipPath,path,true);return releaseDownload.Task;
                };
                api.RefreshTest=(_,_)=>
                {
                    refreshCount++;api.HasChartTest=chartHash=>chartHash==hash;
                    Deliver(new SrvHostShare(share){Session=share.Session+20});
                };
                for(var repeat=0;repeat<100;repeat++)Deliver(new SrvHostShare(share));
                check(controller.TestHasJob&&downloadCount==0,"Share is reserved at native entry before the first download frame");
                controller.TestAdvance();
                check(downloadCount==1,"A burst of 100 identical share notifications starts exactly one download");
                for(var repeat=0;repeat<100;repeat++)
                {
                    Deliver(new SrvHostShare(share){DownloadUrl="https://fixture.invalid/renewed.zip"});
                    Deliver(new SrvHostShare(share){Session=share.Session+1});
                }
                check(downloadCount==1&&controller.TestHasJob,"Repeated URLs and sessions cannot restart or cancel an active chart download");
                releaseDownload.SetResult(true);Drain();
                check(downloadCount==1&&refreshCount==1&&File.ReadAllBytes(target).SequenceEqual(content),
                    "Import refresh reentry downloads/imports the shared chart only once");
                for(var repeat=0;repeat<100;repeat++)Deliver(new SrvHostShare(share));
                check(!controller.TestHasJob&&downloadCount==1,"Completed share sessions cannot reenter the download queue");
                api.HasChartTest=chartHash=>chartHash==hash;
                Deliver(new SrvHostShare(share){Session=share.Session+2});
                check(!controller.TestHasJob&&downloadCount==1&&refreshCount==2,"An exact installed chart refreshes the stale selection without downloading again");
                check(nativeDownloads==0,"Handled duplicate shares never enqueue a native download alongside the plugin");

                // A failed session stays consumed; a deliberate new host share
                // can retry without an automatic error/download storm.
                controller.TestResetShares();api.HasChartTest=_=>false;
                RoomChartController.DownloadFileTest=(_,_)=>{downloadCount++;return Task.FromException(new IOException("Simulated offline share"));};
                Deliver(share);controller.TestAdvance();
                try{controller.TestAdvance();throw new Exception("Expected failed share download");}
                catch(IOException ex){controller.TestFail(ex);}
                var failedCount=downloadCount;
                for(var repeat=0;repeat<100;repeat++)Deliver(new SrvHostShare(share));
                check(downloadCount==failedCount&&!controller.TestHasJob,"A failed session is not retried by duplicate share notifications");
                Deliver(new SrvHostShare(share){Session=share.Session+3});
                check(controller.TestHasJob,"A deliberate new host share session permits a retry after failure");
                controller.TestFail(new IOException("End retry fixture"));
                Deliver(new SrvHostShare(share){Session=share.Session+5});
                check(controller.TestHasJob,"An aborted job before its first frame releases the active share reservation");
                controller.TestFail(new IOException("End aborted-job fixture"));
                controller.TestContext(room,new RoomInfo(info){ChartHash=new string('c',32)});
                Deliver(new SrvHostShare(share){Session=share.Session+4});
                check(!controller.TestHasJob&&nativeDownloads==0,"Delayed shares for an old chart neither download nor fall through to the native queue");
                controller.TestContext(room,new RoomInfo(info){Hostid=998});
                Deliver(share);
                check(controller.TestHasJob,"Changing hosts resets consumed share sessions");
                controller.TestFail(new IOException("End host-change fixture"));
            }
            finally
            {
                guard.UnpatchSelf();controller.TestResetShares();
            }
        }
        finally
        {
            user.SetValue(self,originalUser);api.ResetTests();RoomChartController.DownloadFileTest=null;
            root.SetActive(false);UnityEngine.Object.Destroy(root);Directory.Delete(folder,true);
        }
    }
}
