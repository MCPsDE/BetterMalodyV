using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Malody.Chart;
using Malody.DB;
using Malody.Play;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;

namespace BetterMalodyV.Plugin;

// Actual native controller/interface callbacks; never compiled into a release.
public sealed class ReplayInputProbe : MonoBehaviour
{
    public ReplayInputProbe(IntPtr pointer):base(pointer) { }
    internal int Keys,Indices,Touches,Scores;
    internal readonly List<int> Times=new();
    public void PlayKeyInput(KeyCode key,bool down,int time){Keys++;Times.Add(time);}
    public void PlayIndexedInput(int index,bool down,int time){Indices++;Times.Add(time);}
    public void PlayTouchInput(PlayInput.blr finger,int time){Touches++;Times.Add(time);}
    public void PlayScoreInput(cbq.ScoreUpdate score,int time){Scores++;}
}

internal static class ReplayRuntimeTests
{
    private static string? destination;
    private static bool CaptureNavigation(object __0){destination=__0.ToString();return false;}
    internal static void Run(Action<bool,string> verify)
    {
        void check(bool condition,string label){File.WriteAllText(Path.Combine(BepInEx.Paths.GameRootPath,"replay-test-step.txt"),label);verify(condition,label);}
        var owner=ReplayRunController.Current??throw new Exception("Replay input feature did not bind");var api=owner.Api;
        var parser=ReplayAnalysisGameApi.ResolveParserType();
        check(parser.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Any(m=>m.Name=="CreateFromFilePath"),"Native analysis uses the game's isolated chart parser");
        var framework=typeof(PlayModeType).Assembly;
        var online=framework.GetTypes().Single(t=>t.Namespace=="Malody.Network" && t.GetMethods().Any(m=>m.Name=="get_Self"));
        var self=ReplayGameApi.Call(RoomGameApi.Singleton(online),"get_Self")!;
        var uid=self.GetType().GetProperties().Single(p=>p.PropertyType==typeof(uint));var saved=uid.GetValue(self);
        var root=new GameObject("Replay input verification");root.SetActive(false);
        try
        {
            uid.SetValue(self,(uint)888);
            api.TestLogin=true;
            var replay=new ReplayFile{UserId=888,ChartHash=new string('a',32),Seed=42,Score=new ScoreRecord{Mode=(uint)PlayModeType.Key,Level=2,Pro=true}};
            var record=new ReplayFile.Types.Record{Time=100,Index=0};
            record.Cells.Add(new ReplayFile.Types.Cell{InputKeyDown=(uint)KeyCode.A});
            record.Cells.Add(new ReplayFile.Types.Cell{InputIndexedDown=0});
            record.Cells.Add(new ReplayFile.Types.Cell{InputTouch=new ReplayFile.Types.CellInputTouch{Id=1,Phase=1,State=1}});
            record.Cells.Add(new ReplayFile.Types.Cell{Score=999999});record.Cells.Add(new ReplayFile.Types.Cell{Combo=999});replay.Records.Add(record);
            record=new ReplayFile.Types.Record{Time=200,Index=1};
            record.Cells.Add(new ReplayFile.Types.Cell{InputKeyUp=(uint)KeyCode.A});record.Cells.Add(new ReplayFile.Types.Cell{InputIndexedUp=0});replay.Records.Add(record);
            var inputs=ReplayGameApi.InputsOnly(replay);
            check(inputs.Records[0].Cells.Count==3 && inputs.Score is null,"Reenactment excludes recorded scores, combo and judgement metadata");
            check(replay.Records[0].Cells.Count==5,"Original replay remains unchanged");
            check(api.Eligibility(replay) is null,"Own normal replay passes eligibility");
            api.TestLogin=null;api.Eligibility(replay);check(true,"Actual native login state accessor resolves without changing credentials");api.TestLogin=true;
            api.TestLogin=false;check(api.Eligibility(replay) is not null,"Logged-out sessions cannot start a replay upload run");api.TestLogin=true;
            replay.UserId=889;check(api.Eligibility(replay) is not null,"Another account's replay cannot be submitted");replay.UserId=888;
            replay.Score.Mod=(uint)ModMask.Auto;check(api.Eligibility(replay) is not null,"Auto replay retains native submission restriction");replay.Score.Mod=0;
            var chartType=api.PlayType.GetMethod("get_Chart")!.ReturnType;var chart=Activator.CreateInstance(chartType)!;
            var meta=(ChartRecord)chartType.GetProperties().Single(p=>p.PropertyType==typeof(ChartRecord)).GetValue(chart)!;
            meta.Hash=replay.ChartHash;meta.ChartMode=0x40;meta.Column=4;meta.NoteCount=2;
            var song=Activator.CreateInstance(Plugin.Adapter.SongType)!;
            chartType.GetProperties().Single(p=>p.PropertyType==Plugin.Adapter.SongType && p.CanWrite).SetValue(chart,song);
            api.TestChart=_=>chart;
            var info=api.Prepare(replay);
            check(ReplayGameApi.Call(info,"get_Replay") is null && !(bool)ReplayGameApi.Call(info,"CanIgnoreJudge")!,"Normal play settings use real judgement with no replay score source");
            check((int)ReplayGameApi.Call(info,"get_RandomSeed")! ==42,"Original random seed preserved");
            var playBy=api.PlayType.GetProperties().Single(p=>p.PropertyType==typeof(PlayByType));
            check((PlayByType)playBy.GetValue(info)! ==PlayByType.Player,"Reenactment enters normal play settlement path");
            var calculated=new cbq();calculated.SetPlayInfo((bmf)info);calculated.HandleScoreResult(HitTiming.Best|HitTiming.Combo);
            var calculatedScore=Convert.ToUInt32(ReplayGameApi.Call(calculated,"get_FinalScore"));
            check(calculatedScore>0 && calculatedScore!=999999,"Native score calculation produces new judged points rather than stored replay points");
            check((bool)ReplayGameApi.Call(calculated,"get_TrueScore")!,"Native score eligibility remains intact without overriding it");
            var file=Path.Combine(BepInEx.Paths.GameRootPath,"own-replay-fixture.mrv");
            File.WriteAllBytes(file,Google.Protobuf.MessageExtensions.ToByteArray(replay.Cast<Google.Protobuf.IMessage>()).ToArray());
            check(api.Read(file).UserId==888,"Actual native replay parser reads the selected local file");
            check(Path.GetFileName(api.ReplayArchivePath("own-replay-fixture.mrv"))=="own-replay-fixture.mrv","Native replay archive path resolves through the game config");
            var chartFile=Path.Combine(BepInEx.Paths.GameRootPath,"analysis-chart-fixture.mc");
            File.WriteAllText(chartFile,"{\"meta\":{\"mode\":0,\"version\":\"Lv.5\",\"song\":{\"title\":\"Analysis fixture\",\"artist\":\"Local\"},\"mode_ext\":{\"column\":4}},\"time\":[{\"beat\":[0,0,1],\"bpm\":120}],\"note\":[{\"beat\":[4,0,1],\"column\":0},{\"beat\":[5,0,1],\"endbeat\":[6,0,1],\"column\":1}]}");
            meta.Path=chartFile;meta.Version="Lv.5";meta.Index=0;
            var analyzed=owner.AnalysisApi.Analyze(file);
            check(analyzed.Analysis.ChartNoteCount==2 && analyzed.Analysis.Tracks.Count==4,$"Analysis parses a temporary chart copy and reads track note data: notes={analyzed.Analysis.ChartNoteCount}, tracks={analyzed.Analysis.Tracks.Count}");
            var baka=owner.AnalysisApi.AnalyzeBaka(file);
            check(baka is not null && !baka.Value.TurboTail,"Local replay list uses lowercase baka without Turbo tail judgement");
            var archiveFile=api.ReplayArchivePath("own-replay-fixture.mrv");
            Directory.CreateDirectory(Path.GetDirectoryName(archiveFile)!);
            File.Copy(file,archiveFile,true);
            var itemRoot=new GameObject("Local replay list item fixture");
            var item=itemRoot.AddComponent<Malody.Scene.Item.ItemScore>();
            var row=new GameObject("Local replay row");row.transform.SetParent(itemRoot.transform,false);row.AddComponent<RectTransform>();
            var accObject=new GameObject("ACC");accObject.transform.SetParent(row.transform,false);var acc=accObject.AddComponent<Text>();item.acc=acc;
            var queueMethod=typeof(ReplayRunController).GetMethod("QueueBaka",BindingFlags.Instance|BindingFlags.NonPublic)!;
            queueMethod.Invoke(owner,new object?[]{item,new ScoreRecord{ReplayFile="own-replay-fixture.mrv"}});
            var processMethod=typeof(ReplayRunController).GetMethod("ProcessBakaQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
            processMethod.Invoke(owner,null);
            var label=itemRoot.transform.Find("Local replay row/BetterMalodyV.BakaAccuracy")?.GetComponent<Text>();
            check(label is not null && label.gameObject.activeSelf && label.text.EndsWith("%"),"Local score list item shows the blue Baka accuracy beside ACC");
            UnityEngine.Object.Destroy(itemRoot);
            var turbo=ReplayFile.Parser.ParseFrom(Google.Protobuf.MessageExtensions.ToByteArray(replay.Cast<Google.Protobuf.IMessage>()).ToArray());
            turbo.Score.Custom="E";turbo.Judge=new ReplayFile.Types.Judge{JudgeAtEnd=true};
            var turboFile=Path.Combine(BepInEx.Paths.GameRootPath,"turbo-baka-fixture.mrv");
            File.WriteAllBytes(turboFile,Google.Protobuf.MessageExtensions.ToByteArray(turbo.Cast<Google.Protobuf.IMessage>()).ToArray());
            var turboBaka=owner.AnalysisApi.AnalyzeBaka(turboFile);
            check(turboBaka is not null && turboBaka.Value.TurboTail,"Local replay list switches to lowercase baka-e for Turbo tail judgement");
            check(api.ChartHash(chart)==replay.ChartHash,"Analysis leaves the game's cached chart metadata intact");
            var flipped=ReplayFile.Parser.ParseFrom(Google.Protobuf.MessageExtensions.ToByteArray(replay.Cast<Google.Protobuf.IMessage>()).ToArray());
            flipped.Score.Mod=(uint)ModMask.Flip;
            var flippedFile=Path.Combine(BepInEx.Paths.GameRootPath,"flipped-analysis-fixture.mrv");
            File.WriteAllBytes(flippedFile,Google.Protobuf.MessageExtensions.ToByteArray(flipped.Cast<Google.Protobuf.IMessage>()).ToArray());
            var flippedResult=owner.AnalysisApi.Analyze(flippedFile);
            check(flippedResult.Analysis.Tracks[2].NoteCount==1 && flippedResult.Analysis.Tracks[3].NoteCount==1,"Flip mod mirrors chart lanes before matching replay inputs");
            var luck=ReplayFile.Parser.ParseFrom(Google.Protobuf.MessageExtensions.ToByteArray(replay.Cast<Google.Protobuf.IMessage>()).ToArray());
            luck.Score.Mod=(uint)ModMask.Luck;
            var luckFile=Path.Combine(BepInEx.Paths.GameRootPath,"random-analysis-fixture.mrv");
            File.WriteAllBytes(luckFile,Google.Protobuf.MessageExtensions.ToByteArray(luck.Cast<Google.Protobuf.IMessage>()).ToArray());
            var luckResult=owner.AnalysisApi.Analyze(luckFile);
            check(luckResult.Analysis.Profiles.Count==0 && luckResult.Analysis.Warnings.Any(w=>w.Contains("Luck")),"Unknown random lane permutations suppress misleading timing estimates");
            var previous=api.InUse;var guard=new Harmony("com.mcpsde.bettermalodyv.verify.replay-navigation");
            try
            {
                guard.Patch(ReplayGameApi.Navigation,prefix:new HarmonyMethod(typeof(ReplayRuntimeTests),nameof(CaptureNavigation)));
                owner.Launch(file);
                check(destination=="Play" && ReplayGameApi.Call(api.InUse,"get_Replay") is null,"Reenactment launch selects real play scene with normal game settings");
            }
            finally {owner.TestClear();api.Use(previous);guard.UnpatchSelf();}
            var input=root.AddComponent<PlayInput>();var player=root.AddComponent<ChartPlayer>();player.playInput=input;
            ClassInjector.RegisterTypeInIl2Cpp<ReplayInputProbe>(new RegisterTypeOptions{Interfaces=new Il2CppInterfaceCollection(new[]{typeof(ChartPlayer.bul)})});
            var probe=root.AddComponent<ReplayInputProbe>();api.TestControl(input,probe.Cast<ChartPlayer.bul>());
            owner.TestArm(info,replay,player);
            api.InputTick.Invoke(input,new object[]{99});check(probe.Keys==0 && probe.Indices==0,"Future replay inputs are not emitted early");
            api.InputTick.Invoke(input,new object[]{100});check(probe.Keys==1 && probe.Indices==1 && probe.Touches==1,"Normal input tick receives original keyboard, indexed and touch inputs");
            api.InputTick.Invoke(input,new object[]{200});check(probe.Keys==2 && probe.Indices==2 && probe.Scores==0,"Input reenactment never injects recorded score values");
            check(probe.Times.SequenceEqual(new[]{100,100,100,200,200}),"Recorded input timestamps and order preserved");
            api.InputReset.Invoke(input,new object[]{201});api.InputTick.Invoke(input,new object[]{202});
            check(owner.TestFailure is null && probe.Keys==2 && probe.Indices==2,"Native seek after the final input stays valid and emits no extra input");
            var key=api.PhysicalInputs.Single(m=>m.GetParameters()[0].ParameterType==typeof(KeyCode));
            key.Invoke(input,new object[]{KeyCode.B,true,201});check(probe.Keys==2,"Physical gameplay input cannot contaminate active reenactment");
            api.ResetRandom.Invoke(info,new object[]{0});check((int)ReplayGameApi.Call(info,"get_RandomSeed")! ==42,"Native restart keeps the original random layout seed");
            owner.TestClear();key.Invoke(input,new object[]{KeyCode.B,true,202});check(probe.Keys==3,"Normal physical input works outside the selected reenactment");
            var forbidden=Harmony.GetAllPatchedMethods().Any(m=>m.DeclaringType==typeof(cbq) || m.DeclaringType?.Name=="Guardian" || m.Name=="Submit");
            check(!forbidden,"Score calculation, Guardian and native submission methods remain unpatched");
        }
        finally {owner.TestClear();api.TestChart=null;api.TestLogin=null;uid.SetValue(self,saved);UnityEngine.Object.Destroy(root);}
    }
}
