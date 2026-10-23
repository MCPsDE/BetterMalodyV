using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Malody.DB;
using Malody.Play;
using Malody.Scene;
using Malody.Scene.Panel;

namespace BetterMalodyV.Plugin;

// Only the supported 6.7 input aliases live here; no score or Guardian patches.
internal sealed class ReplayGameApi
{
    private const BindingFlags Any=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    internal Type PlayType { get; }=typeof(ChartPlayer).GetMethod("Load")!.GetParameters()[0].ParameterType;
    internal MethodInfo InputTick { get; }=InputMethod("bwb",typeof(int));
    internal MethodInfo InputReset { get; }=InputMethod("bwa",typeof(int));
    internal MethodInfo ResetRandom { get; }
    internal MethodInfo[] PhysicalInputs { get; }
    private readonly Type playback;
    private readonly PropertyInfo inputControl,seed,rawReplay,playBy,playFrom,mode,pro;
    private readonly PropertyInfo localPath=typeof(PanelScoreDetail).GetProperties(Any|BindingFlags.DeclaredOnly).Single(p=>p.PropertyType==typeof(string));
    private readonly Type onlineInfo;
    private readonly MethodInfo helperAttach,helperSeek,helperTick;
    private readonly Type http;
    private readonly PropertyInfo onlineFlag;
    internal static readonly MethodInfo Navigation=typeof(ScenePlay).Assembly.GetTypes().SelectMany(t=>t.GetMethods(Any)).Single(m=>m.Name=="ToScene" && m.IsStatic && m.GetParameters().Length==1);
    internal ReplayGameApi()
    {
        var control=typeof(PlayInput).GetProperties(Any).Select(p=>p.PropertyType).Single(t=>t.DeclaringType==typeof(ChartPlayer) && t.GetMethods().Any(m=>m.Name=="PlayScoreInput"));
        inputControl=typeof(PlayInput).GetProperties(Any).Single(p=>p.PropertyType==control);
        playback=typeof(ChartPlayer).GetNestedTypes(Any).Single(t=>t.GetConstructors().Any(c=>c.GetParameters() is var p && p.Length==1 && p[0].ParameterType==typeof(ReplayFile)));
        foreach(var name in new[]{"AttachController","ResetControl","UpdateControl"})if(playback.GetMethod(name) is null)throw new NotSupportedException("Replay input API changed: "+name);
        helperAttach=playback.GetMethod("AttachController")!;helperSeek=playback.GetMethod("ResetControl")!;helperTick=playback.GetMethod("UpdateControl")!;
        rawReplay=PlayType.GetProperty("bzt",Any)!;
        if(rawReplay?.PropertyType!=typeof(ReplayFile))throw new NotSupportedException("Replay settings field changed");
        seed=PlayType.GetProperties(Any).Single(p=>p.PropertyType==typeof(int) && p.Name.Contains("BackingField"));
        playBy=Unique(typeof(PlayByType));playFrom=Unique(typeof(PlayFrom));mode=Unique(typeof(Malody.Chart.PlayModeType));
        pro=PlayType.GetProperty("tie",Any)!;
        if(pro?.PropertyType!=typeof(bool))throw new NotSupportedException("Replay Pro setting changed");
        ResetRandom=PlayType.GetMethod("ResetRandom")!;
        PhysicalInputs=typeof(PlayInput).GetMethods(Any).Where(m=>m.ReturnType==typeof(void) && m.GetParameters() is var p &&
            (p.Length==3 && p[1].ParameterType==typeof(bool) && p[2].ParameterType==typeof(int) && (p[0].ParameterType==typeof(int) || p[0].ParameterType==typeof(UnityEngine.KeyCode)) ||
             p.Length==2 && p[0].ParameterType.IsNested && p[0].ParameterType.DeclaringType==typeof(PlayInput) && p[1].ParameterType==typeof(int))).ToArray();
        if(PhysicalInputs.Length!=3)throw new NotSupportedException("Physical input API changed");
        onlineInfo=typeof(Malody.Chart.PlayModeType).Assembly.GetTypes().Single(t=>t.Namespace=="Malody.Network" && t.GetMethods(Any).Any(m=>m.Name=="get_Self"));
        http=typeof(Malody.Chart.PlayModeType).Assembly.GetTypes().Single(t=>t.Namespace=="Malody.Network" && t.GetMethods(Any).Any(m=>m.Name=="SendRequest" && m.GetParameters().Length==3));
        onlineFlag=http.GetProperties(Any).Single(p=>p.PropertyType.IsGenericType && p.PropertyType.GenericTypeArguments.SequenceEqual(new[]{typeof(bool)}));
    }
    private static MethodInfo InputMethod(string name,Type argument)
    {
        var method=typeof(PlayInput).GetMethod(name,Any,null,new[]{argument},null)!;
        if(method is null || method.ReturnType!=typeof(void))throw new NotSupportedException("Normal input controller changed: "+name);
        return method;
    }
    private PropertyInfo Unique(Type type)=>PlayType.GetProperties(Any).Single(p=>p.PropertyType==type);
    internal object Local=>CallStatic(PlayType,"get_Local")!;
    internal object InUse=>CallStatic(PlayType,"get_InUse")!;
    internal object? Chart(object info)=>Call(info,"get_Chart");
    internal uint UserId
    {
        get {var self=Call(RoomGameApi.Singleton(onlineInfo),"get_Self")!;return Convert.ToUInt32(self.GetType().GetProperties(Any).Single(p=>p.PropertyType==typeof(uint)).GetValue(self));}
    }
    internal string? LocalPath(PanelScoreDetail panel)=>(string?)localPath.GetValue(panel);
    internal static object? Call(object value,string method,params object?[] args)=>value.GetType().GetMethod(method,Any)!.Invoke(value,args);
    private static object? CallStatic(Type type,string method,params object?[] args)=>type.GetMethod(method,Any)!.Invoke(null,args);
    internal static bool Same(object? a,object? b)=>a is Il2CppObjectBase x && b is Il2CppObjectBase y && x.Pointer==y.Pointer;
    internal string ChartHash(object chart)=>((ChartRecord)chart.GetType().GetProperties(Any).Single(p=>p.PropertyType==typeof(ChartRecord)).GetValue(chart)!).Hash;
    internal ReplayFile Read(string file)
    {
        if(!file.EndsWith(".mrv",StringComparison.OrdinalIgnoreCase))throw new IOException("仅支持当前格式的 .mrv 回放。");
        if(new FileInfo(file).Length>256L*1024*1024)throw new IOException("回放文件过大。");
        return ReplayFile.Parser.ParseFrom(File.ReadAllBytes(file));
    }
    internal string? Eligibility(ReplayFile replay)
    {
        if(UserId==0 || !LoggedIn())return "请先登录回放所属账号。";
        if(replay.UserId==0 || replay.UserId!=UserId)return "此回放不属于当前登录账号。";
        if(replay.Score is null || replay.Records.Count==0 || replay.ChartHash.Length!=32)return "回放数据不完整。";
        if(!string.IsNullOrEmpty(replay.Score.Custom) || replay.Judge is not null ||
           ((ModMask)replay.Score.Mod & (ModMask.Auto|ModMask.Less|ModMask.Taps|ModMask.Holds))!=0)return "该回放的设置不符合正常成绩上传条件。";
        return null;
    }
    private bool LoggedIn()
    {
#if BETTER_MALODYV_VERIFY
        if(TestLogin.HasValue)return TestLogin.Value;
#endif
        return (bool)Call(onlineFlag.GetValue(RoomGameApi.Singleton(http))!,"get_Value")!;
    }
    internal object Prepare(ReplayFile replay)
    {
        var error=Eligibility(replay);if(error is not null)throw new IOException(error);
        var chart=ResolveChart(replay.ChartHash);
        if(chart is null || ChartHash(chart)!=replay.ChartHash)throw new IOException("缺少与回放哈希一致的本地谱面。");
        var info=Call(Local,"Dump")!;Call(info,"ResetForNormal");Call(info,"set_Chart",chart);Call(info,"set_Replay",replay);
        playBy.SetValue(info,PlayByType.Player);playFrom.SetValue(info,PlayFrom.Normal);
        if(replay.Score.Mode!=0)mode.SetValue(info,(Malody.Chart.PlayModeType)replay.Score.Mode);
        if(Navigation.DeclaringType!.GetMethod("PlayModeToScenePlay")!.Invoke(null,new[]{mode.GetValue(info)}) is null)throw new IOException("该回放的玩法当前不受支持。");
        if(!(bool)Call(info,"Validate")! || (uint)(ModMask)Call(info,"get_ModForPlay")! !=replay.Score.Mod)throw new IOException("当前游戏无法保持回放的原始玩法设置。");
        rawReplay.SetValue(info,null);seed.SetValue(info,replay.Seed);pro.SetValue(info,replay.Score.Pro);
        return info;
    }
    internal void Use(object info)=>CallStatic(PlayType,"set_InUse",info);
    internal static void Navigate(string scene)
    {
        Navigation.Invoke(null,new[]{Enum.Parse(Navigation.GetParameters()[0].ParameterType,scene)});
    }
    internal void KeepSeed(object info,int original)=>seed.SetValue(info,original);
    internal object Helper(ReplayFile inputOnly)=>Activator.CreateInstance(playback,new object[]{inputOnly})!;
    internal object? Control(PlayInput input)=>inputControl.GetValue(input);
    internal void Attach(object helper,object control)=>helperAttach.Invoke(helper,new[]{control});
    internal void Seek(object helper,int time)=>helperSeek.Invoke(helper,new object[]{time});
    internal void Tick(object helper,int time)=>helperTick.Invoke(helper,new object[]{time});
    internal object? ResolveChart(string hash)
    {
#if BETTER_MALODYV_VERIFY
        if(TestChart is not null)return TestChart(hash);
#endif
        return Plugin.Adapter.ManagerType.GetMethod("GetChartByHash")!.Invoke(RoomGameApi.Singleton(Plugin.Adapter.ManagerType),new object[]{hash});
    }
#if BETTER_MALODYV_VERIFY
    internal bool? TestLogin;
    internal Func<string,object?>? TestChart;
    internal void SetLocalPath(PanelScoreDetail panel,string value)=>localPath.SetValue(panel,value);
    internal void TestControl(PlayInput input,object value)=>inputControl.SetValue(input,value);
#endif
    internal static ReplayFile InputsOnly(ReplayFile replay)
    {
        var output=new ReplayFile();var count=0;
        for(var i=0;i<replay.Records.Count;i++)
        {
            var source=replay.Records[i];var record=new ReplayFile.Types.Record{Time=source.Time,Index=source.Index};
            for(var j=0;j<source.Cells.Count;j++)
            {
                var cell=source.Cells[j];
                if(cell.CellCase is ReplayFile.Types.Cell.CellOneofCase.InputKeyDown or ReplayFile.Types.Cell.CellOneofCase.InputKeyUp or
                    ReplayFile.Types.Cell.CellOneofCase.InputIndexedDown or ReplayFile.Types.Cell.CellOneofCase.InputIndexedUp or ReplayFile.Types.Cell.CellOneofCase.InputTouch)
                {record.Cells.Add(cell);count++;}
            }
            if(record.Cells.Count>0)output.Records.Add(record);
        }
        if(count==0)throw new IOException("该回放没有可重演的操作记录。");
        // Native ResetControl uses FindIndex; retain a future cursor even when
        // a pause/resume seek lands after the final real input.
        output.Records.Add(new ReplayFile.Types.Record{Time=int.MaxValue,Index=int.MaxValue});
        return output;
    }
}
