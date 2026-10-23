using System.Reflection;
using BetterMalodyV.Shared;
using Il2CppInterop.Runtime.InteropTypes;
using Malody.Chart;
using Malody.DB;
using Malody.Play;

namespace BetterMalodyV.Plugin;

internal sealed class ReplayAnalysisGameApi(ReplayGameApi replayApi)
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type? parserType;

    internal static Type ResolveParserType() => parserType ??= typeof(BeatOffset).Assembly.GetType("Malody.Chart.isf", true)!;

    internal ReplayAnalysisSession Analyze(string replayPath)
    {
        var replay = replayApi.Read(replayPath);
        if (replay.Score is null || replay.ChartHash.Length != 32)
            throw new InvalidDataException("回放缺少有效的谱面或成绩信息。");

        var cachedChart = replayApi.ResolveChart(replay.ChartHash);
        if (cachedChart is null || replayApi.ChartHash(cachedChart) != replay.ChartHash)
            throw new FileNotFoundException("本地没有找到与回放哈希一致的谱面。");

        var record = (ChartRecord?)cachedChart.GetType().GetProperties(Any)
            .Single(property => property.PropertyType == typeof(ChartRecord)).GetValue(cachedChart);
        if (record is null) throw new InvalidDataException("本地谱面信息不完整。");
        var chartPath = FirstNonEmpty((string?)ReadMember(cachedChart, "FilePath"), (string?)ReadMember(record, "Path"));
        if (string.IsNullOrWhiteSpace(chartPath) || !File.Exists(chartPath) && !chartPath.Contains("jar:", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("找不到回放对应的本地谱面文件：" + (chartPath ?? "(empty)"), chartPath);

        var chart = ParseUncachedChart(cachedChart);

        var modeValue = checked((int)replay.Score.Mode);
        var modeName = Enum.IsDefined(typeof(PlayModeType), modeValue)
            ? Enum.GetName(typeof(PlayModeType), modeValue) ?? "Unknown"
            : "Unknown";
        var mods = (ModMask)replay.Score.Mod;
        var laneMode = modeValue is (int)PlayModeType.Key or (int)PlayModeType.Step or (int)PlayModeType.DJ;
        var supportsCvt = laneMode && !mods.HasFlag(ModMask.Luck);
        var laneCount = checked((int)record.Column);
        var chartNotes = supportsCvt ? ReadChartNotes(chart, laneCount) : Array.Empty<ReplayChartNote>();
        if (supportsCvt && mods.HasFlag(ModMask.Flip))
            chartNotes = chartNotes.Select(note => note with { Lane = laneCount - note.Lane - 1 }).ToArray();
        var actions = new List<ReplayInputAction>();
        var hits = new List<ReplayHitEvent>();
        for (var recordIndex = 0; recordIndex < replay.Records.Count; recordIndex++)
        {
            var item = replay.Records[recordIndex];
            for (var cellIndex = 0; cellIndex < item.Cells.Count; cellIndex++)
            {
                var cell = item.Cells[cellIndex];
                if (cell.CellCase == ReplayFile.Types.Cell.CellOneofCase.InputIndexedDown)
                    actions.Add(new(item.Time, checked((int)cell.InputIndexedDown), true));
                else if (cell.CellCase == ReplayFile.Types.Cell.CellOneofCase.InputIndexedUp)
                    actions.Add(new(item.Time, checked((int)cell.InputIndexedUp), false));
                else if (cell.CellCase == ReplayFile.Types.Cell.CellOneofCase.HitNote)
                    hits.Add(new(item.Time, checked((int)cell.HitNote.Index), cell.HitNote.Timing));
            }
        }

        if (laneCount <= 0) laneCount = actions.Count == 0 ? 1 : actions.Max(action => action.Lane) + 1;
        laneCount = Math.Clamp(laneCount, 1, 128);
        var analysis = ReplayAnalysisEngine.Analyze(chartNotes, actions, hits, laneCount);
        if (!laneMode)
            analysis = analysis with
            {
                Profiles = Array.Empty<ReplayProfileScore>(),
                Warnings = analysis.Warnings.Append("该玩法暂不支持 CVT 轨道换算；本地判定与按压事件仍来自原始回放。").ToArray()
            };
        else if (mods.HasFlag(ModMask.Luck))
            analysis = analysis with
            {
                Profiles = Array.Empty<ReplayProfileScore>(),
                Warnings = analysis.Warnings.Append("Luck 随机轨道无法从成绩记录恢复；为避免错误匹配，隐藏轨道偏移和 CVT 估分。").ToArray()
            };
        else if (chartNotes.Length == 0)
            analysis = analysis with { Warnings = analysis.Warnings.Append("谱面解析后没有可用的轨道音符，未生成判定换算结果。").ToArray() };
        if ((mods & (ModMask.Less | ModMask.Taps | ModMask.Holds)) != 0)
            analysis = analysis with { Warnings = analysis.Warnings.Append("此回放启用了会改变谱面对象或时序的 Mod；CVT 结果按本地原谱面估算，可能与实际游玩对象数不同。").ToArray() };
        if (replay.Judge is not null || !string.IsNullOrEmpty(replay.Score.Custom))
            analysis = analysis with { Warnings = analysis.Warnings.Append("回放保存了自定义判定设置；CVT 结果使用默认判定档位，仅供比较。").ToArray() };

        var metadata = ReadChartRecord(chart);
        var title = FirstNonEmpty((string?)ReadMember(metadata, "Title"), replay.Title, "未知谱面");
        var artist = FirstNonEmpty((string?)ReadMember(metadata, "Artist"), replay.Artist);
        var difficulty = FirstNonEmpty((string?)ReadMember(metadata, "Version"), replay.Version);
        var rankValue = checked((int)replay.Score.Rank);
        var rank = Enum.IsDefined(typeof(ScoreRank), rankValue) ? Enum.GetName(typeof(ScoreRank), rankValue)! : rankValue.ToString();

        return new(
            title,
            artist,
            difficulty,
            Path.GetFileName(replayPath),
            modeName,
            replay.Score.Score,
            replay.Score.Combo,
            replay.Score.Acc,
            rank,
            analysis);
    }

    private static object ParseUncachedChart(object cachedChart)
    {
        var parserType = ResolveParserType();
        var cloneMethod = cachedChart.GetType().GetMethod("CloneWithMeta", Any, null, Type.EmptyTypes, null)
            ?? throw new NotSupportedException("当前游戏版本缺少谱面元数据克隆接口。");
        var clone = cloneMethod.Invoke(cachedChart, null)
            ?? throw new InvalidDataException("无法创建用于分析的谱面副本。");
        if (ReplayGameApi.Same(cachedChart, clone)) throw new InvalidDataException("谱面副本与游戏缓存对象相同，已停止分析。");
        var factory = parserType.GetMethods(Any).SingleOrDefault(method => method.IsStatic && method.Name == "CreateForExistChart"
            && method.GetParameters() is var parameters && parameters.Length == 1 && parameters[0].ParameterType == clone.GetType())
            ?? throw new NotSupportedException("当前游戏版本缺少现有谱面解析接口。");
        var parser = factory.Invoke(null, new[] { clone })
            ?? throw new InvalidDataException("游戏无法重新解析该谱面文件。");
        var parse = parserType.GetMethod("Parse", Any, null, new[] { typeof(bool) }, null)
            ?? throw new NotSupportedException("当前游戏版本的谱面解析接口已变化。");
        if (!(bool)parse.Invoke(parser, new object[] { false })!)
            throw new InvalidDataException("游戏无法完整解析回放对应的谱面。");
        return clone;
    }

    private static string ChartHash(object chart)
    {
        var record = ReadChartRecord(chart);
        return record is null ? string.Empty : (string?)ReadMember(record, "Hash") ?? string.Empty;
    }

    private static ChartRecord? ReadChartRecord(object chart) =>
        chart.GetType().GetProperties(Any).Single(property => property.PropertyType == typeof(ChartRecord)).GetValue(chart) as ChartRecord;

    private static ReplayChartNote[] ReadChartNotes(object chart, int laneCount)
    {
        var list = ReadNoteList(chart) ?? throw new InvalidDataException("解析后的谱面缺少音符列表。");
        var count = TryReadCount(list);
        if (count < 0) throw new InvalidDataException("游戏音符列表无法读取。");
        var item = list.GetType().GetProperty("Item", Any)
            ?? throw new InvalidDataException("游戏音符列表无法读取。");
        var notes = new List<ReplayChartNote>(count);
        for (var index = 0; index < count; index++)
        {
            var note = item.GetValue(list, new object[] { index });
            if (note is null) continue;
            var lane = ReadLane(note);
            if (lane is null || lane < 0 || lane >= laneCount) continue;
            var start = ReadMember(note, "start");
            if (start is null) continue;
            var time = ReadNumber(ReadMember(start, "OffsetInt", "offset"));
            if (time is null) continue;
            var end = ReadMember(note, "end");
            var endTime = end is null ? null : ReadNumber(ReadMember(end, "OffsetInt", "offset"));
            notes.Add(new(time.Value, lane.Value, endTime));
        }
        return notes.OrderBy(note => note.TimeMs).ThenBy(note => note.Lane).ToArray();
    }

    private static object? ReadNoteList(object chart)
    {
        var named = ReadMember(chart, "noteList");
        if (named is not null) return named;
        var noteListProperties = chart.GetType().GetProperties(Any)
            .Where(property => property.GetIndexParameters().Length == 0 && property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition().FullName == "Il2CppSystem.Collections.Generic.List`1"
                && property.PropertyType.GenericTypeArguments[0].Name == "Note")
            .OrderBy(property => property.MetadataToken);
        return noteListProperties
            .Select(property => property.GetValue(chart))
            .FirstOrDefault(value => value is not null);
    }

    private static int? ReadLane(object note)
    {
        if (note is Il2CppObjectBase native)
        {
            var laneInterface = native.TryCast<iqy.iqm>();
            if (laneInterface is not null)
            {
                var laneProperty = laneInterface.GetType().GetProperties(Any)
                    .Single(property => property.PropertyType == typeof(short) && property.GetMethod is not null);
                return Convert.ToInt32(laneProperty.GetValue(laneInterface));
            }
        }
        foreach (var name in new[] { "X", "column", "index" })
        {
            var value = ReadMember(note, name);
            if (value is null) continue;
            try { return Convert.ToInt32(value); }
            catch { }
        }
        return null;
    }

    private static int TryReadCount(object? list)
    {
        if (list is null) return -1;
        var value = ReadMember(list, "Count");
        if (value is null) return -1;
        try { return Convert.ToInt32(value); }
        catch { return -1; }
    }

    private static double? ReadNumber(object? value)
    {
        if (value is null) return null;
        try
        {
            var number = Convert.ToDouble(value);
            return double.IsFinite(number) ? number : null;
        }
        catch { return null; }
    }

    private static object? ReadMember(object? target, params string[] names)
    {
        if (target is null) return null;
        foreach (var name in names)
        {
            for (var type = target.GetType(); type is not null; type = type.BaseType)
            {
                var property = type.GetProperty(name, Any | BindingFlags.DeclaredOnly);
                if (property is not null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(target);
                var field = type.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (field is not null) return field.GetValue(target);
            }
        }
        return null;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
