namespace BetterMalodyV.Shared;

public static class ReplayAnalysisEngine
{
    private enum ScoreFamily { Quadratic, StudentT, Windows, Wife }

    private sealed record Profile(
        string Group,
        string Label,
        ScoreFamily Family,
        double DefaultWindow,
        double[] Windows,
        double[] Weights,
        (double Early, double Late)[] TimingWindows,
        bool IncludeTail = false,
        int WifeVersion = 4,
        string WindowLabel = "");

    private sealed record MatchedNote(int NoteIndex, double Error);
    private sealed record ScoredObject(double TimeMs, double Accuracy);

    private static readonly double[] OffsetBounds = Enumerable.Range(0, 19).Select(i => -180d + i * 20).ToArray();
    private static readonly double[] DurationBounds = { 0, 20, 40, 60, 80, 100, 150, 200, 300, 500, double.PositiveInfinity };
    private static readonly double[] IntervalBounds = { 0, 50, 75, 100, 125, 150, 200, 300, 500, 1000, double.PositiveInfinity };

    public static ReplayAnalysisResult Analyze(
        IReadOnlyList<ReplayChartNote> chartNotes,
        IReadOnlyList<ReplayInputAction> inputActions,
        IReadOnlyList<ReplayHitEvent> hitEvents,
        int laneCount,
        ReplayJudgementFlags? judgementFlags = null)
    {
        ArgumentNullException.ThrowIfNull(chartNotes);
        ArgumentNullException.ThrowIfNull(inputActions);
        ArgumentNullException.ThrowIfNull(hitEvents);
        laneCount = Math.Clamp(laneCount, 1, 128);

        var chart = chartNotes
            .Where(note => note.Lane >= 0 && note.Lane < laneCount && double.IsFinite(note.TimeMs)
                && (note.HoldEndMs is null || double.IsFinite(note.HoldEndMs.Value)))
            .OrderBy(note => note.TimeMs)
            .ThenBy(note => note.Lane)
            .ToArray();
        var actions = inputActions
            .Select((action, order) => (Action: action, Order: order))
            .Where(item => item.Action.Lane >= 0 && item.Action.Lane < laneCount && double.IsFinite(item.Action.TimeMs))
            .OrderBy(item => item.Action.TimeMs)
            .ThenBy(item => item.Order)
            .Select(item => item.Action)
            .ToArray();
        var hits = hitEvents.Where(hit => hit.Lane >= 0 && hit.Lane < laneCount && double.IsFinite(hit.TimeMs)).ToArray();

        var calibration = EstimateChartOffset(chart, actions.Where(action => action.IsDown).ToArray());
        var offsets = MatchHeads(chart, actions, -180, 180, calibration)
            .Values.Select(match => match.Error).OrderBy(value => value).ToArray();
        var profiles = DefaultProfiles().Select(profile => ScoreProfile(chart, actions, profile, calibration)).ToArray();
        var matchedCount = offsets.Length;
        var tracks = new ReplayTrackAnalysis[laneCount];
        for (var lane = 0; lane < laneCount; lane++)
        {
            var laneOffsets = MatchHeads(chart, actions, -180, 180, calibration, lane)
                .Values.Select(match => match.Error).OrderBy(value => value).ToArray();
            tracks[lane] = new(
                lane,
                chart.Count(note => note.Lane == lane),
                BuildStats(laneOffsets),
                BuildHistogram(laneOffsets, OffsetBounds, "ms"),
                BuildPressStats(actions, lane));
        }

        var warnings = new List<string>();
        if (chart.Length == 0) warnings.Add("谱面没有可用于分析的音符。");
        if (actions.Length == 0) warnings.Add("回放没有可用于逐轨分析的按下/抬起事件。");
        if (chart.Length > 0 && actions.Length > 0 && matchedCount == 0)
            warnings.Add("没有找到可匹配的音符，请确认回放和谱面版本一致。");

        return new(
            chart.Length,
            matchedCount,
            Math.Max(0, chart.Length - matchedCount),
            actions.Length,
            calibration,
            BuildStats(offsets),
            BuildHistogram(offsets, OffsetBounds, "ms"),
            BuildPressStats(actions, null),
            BuildJudgements(hits, judgementFlags ?? GameJudgementFlags),
            tracks,
            profiles,
            warnings);
    }

    public static ReplayJudgementFlags GameJudgementFlags { get; } = new(
        Best: 1 << 1,
        Cool: 1 << 2,
        Good: 1 << 3,
        Bad: 1 << 4,
        Miss: 1 << 0,
        Early: 1 << 6,
        Late: 1 << 5);

    private static ReplayJudgementTally BuildJudgements(IReadOnlyList<ReplayHitEvent> hits, ReplayJudgementFlags flags)
    {
        static int Count(IReadOnlyList<ReplayHitEvent> values, int mask) =>
            mask == 0 ? 0 : values.Count(value => (value.TimingFlags & mask) != 0);

        return new(
            Count(hits, flags.Best), Count(hits, flags.Cool), Count(hits, flags.Good), Count(hits, flags.Bad),
            Count(hits, flags.Miss), Count(hits, flags.Early), Count(hits, flags.Late));
    }

    private static ReplayPressStats BuildPressStats(IReadOnlyList<ReplayInputAction> actions, int? lane)
    {
        var selected = lane is null ? actions : actions.Where(action => action.Lane == lane.Value).ToArray();
        var durations = new List<double>();
        var intervals = new List<double>();
        var unmatchedUps = 0;
        var unmatchedDowns = 0;
        var pressCount = 0;
        var lastDown = new Dictionary<int, double>();
        var heldSince = new Dictionary<int, double>();

        foreach (var action in selected)
        {
            if (action.IsDown)
            {
                if (heldSince.ContainsKey(action.Lane)) continue;
                pressCount++;
                if (lastDown.TryGetValue(action.Lane, out var previous) && action.TimeMs >= previous)
                    intervals.Add(action.TimeMs - previous);
                lastDown[action.Lane] = action.TimeMs;
                heldSince[action.Lane] = action.TimeMs;
            }
            else if (heldSince.Remove(action.Lane, out var start))
            {
                if (action.TimeMs >= start) durations.Add(action.TimeMs - start);
                else unmatchedUps++;
            }
            else unmatchedUps++;
        }

        unmatchedDowns = heldSince.Count;
        return new(
            pressCount,
            durations.Count,
            unmatchedDowns,
            unmatchedUps,
            BuildHistogram(durations, DurationBounds, "ms"),
            BuildHistogram(intervals, IntervalBounds, "ms"));
    }

    private static ReplayValueStats BuildStats(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return new(0, 0, 0, 0, null, null, 0, 0, null, null);
        var total = values.Sum();
        var early = values.Where(value => value < 0).ToArray();
        var late = values.Where(value => value > 0).ToArray();
        var average = total / values.Count;
        var variance = values.Sum(value => Math.Pow(value - average, 2)) / values.Count;
        var sorted = values.OrderBy(value => value).ToArray();
        var middle = sorted.Length / 2;
        var median = sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
        return new(
            values.Count,
            total,
            average,
            values.Sum(Math.Abs) / values.Count,
            early.Length == 0 ? null : early.Average(),
            late.Length == 0 ? null : late.Average(),
            median,
            Math.Sqrt(variance),
            sorted[0],
            sorted[^1]);
    }

    private static ReplayHistogram BuildHistogram(IReadOnlyList<double> values, IReadOnlyList<double> bounds, string unit)
    {
        var counts = new int[bounds.Count - 1];
        var edges = bounds.ToArray();
        foreach (var value in values)
        {
            if (!double.IsFinite(value) || value < bounds[0] || value > bounds[^1]) continue;
            var index = Array.BinarySearch(edges, value);
            if (index >= 0) index = Math.Min(index, counts.Length - 1);
            else index = ~index - 1;
            if (index >= 0 && index < counts.Length) counts[index]++;
        }

        var labels = new string[counts.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var left = bounds[i];
            var right = bounds[i + 1];
            labels[i] = double.IsPositiveInfinity(right)
                ? $"{left:0}+ {unit}"
                : $"{left:0}–{right:0} {unit}";
        }
        return new(labels, counts);
    }

    private static Dictionary<int, MatchedNote> MatchHeads(
        IReadOnlyList<ReplayChartNote> chart,
        IReadOnlyList<ReplayInputAction> actions,
        double earlyWindow,
        double lateWindow,
        double calibration,
        int? laneFilter = null)
    {
        var byLane = new Dictionary<int, List<int>>();
        for (var i = 0; i < chart.Count; i++)
        {
            var note = chart[i];
            if (laneFilter is not null && note.Lane != laneFilter.Value) continue;
            if (!byLane.TryGetValue(note.Lane, out var list)) byLane.Add(note.Lane, list = new());
            list.Add(i);
        }

        var cursors = new Dictionary<int, int>();
        var heads = new Dictionary<int, MatchedNote>();
        foreach (var action in actions)
        {
            if (!action.IsDown || (laneFilter is not null && action.Lane != laneFilter.Value)
                || !byLane.TryGetValue(action.Lane, out var indices)) continue;
            var cursor = cursors.GetValueOrDefault(action.Lane);
            var pressTime = action.TimeMs + calibration;
            while (cursor < indices.Count && chart[indices[cursor]].TimeMs < pressTime - lateWindow) cursor++;
            var candidate = cursor < indices.Count ? indices[cursor] : -1;
            if (candidate < 0) continue;
            var error = pressTime - chart[candidate].TimeMs;
            if (error < earlyWindow || error > lateWindow) continue;
            cursors[action.Lane] = cursor + 1;
            heads[candidate] = new(candidate, error);
        }
        return heads;
    }

    private static double EstimateChartOffset(IReadOnlyList<ReplayChartNote> chart, IReadOnlyList<ReplayInputAction> downs)
    {
        if (chart.Count == 0 || downs.Count < 8 || chart[0].TimeMs <= 1) return 0;
        var timesByLane = new Dictionary<int, List<double>>();
        foreach (var note in chart)
        {
            if (!timesByLane.TryGetValue(note.Lane, out var values)) timesByLane.Add(note.Lane, values = new());
            values.Add(note.TimeMs);
        }

        var baseScore = OffsetMatchScore(downs, timesByLane, 0);
        if (baseScore.Matched >= Math.Floor(Math.Min(downs.Count, 2048) * .985) && baseScore.MeanError <= 3) return 0;

        var candidates = new HashSet<double> { 0, Math.Round(chart[0].TimeMs, 3) };
        foreach (var press in downs.Take(512))
        {
            if (!timesByLane.TryGetValue(press.Lane, out var values)) continue;
            var low = LowerBound(values, press.TimeMs);
            for (var index = Math.Max(0, low - 2); index < Math.Min(values.Count, low + 3); index++)
            {
                var offset = values[index] - press.TimeMs;
                if (Math.Abs(offset) <= 120000) candidates.Add(Math.Round(offset, 3));
            }
        }

        var bins = new Dictionary<int, (int Count, double Sum)>();
        foreach (var press in downs.Take(192))
        {
            if (!timesByLane.TryGetValue(press.Lane, out var values)) continue;
            var step = Math.Max(1, (int)Math.Ceiling(values.Count / 4096d));
            for (var index = 0; index < values.Count; index += step)
            {
                var offset = values[index] - press.TimeMs;
                if (Math.Abs(offset) > 120000) continue;
                var key = (int)Math.Round(offset / 2);
                var current = bins.GetValueOrDefault(key);
                bins[key] = (current.Count + 1, current.Sum + offset);
            }
        }
        foreach (var bin in bins.OrderByDescending(item => item.Value.Count).ThenBy(item => Math.Abs(item.Key)).Take(48))
            candidates.Add(Math.Round(bin.Value.Sum / bin.Value.Count, 3));

        var bestOffset = 0d;
        var best = baseScore;
        foreach (var offset in candidates)
        {
            var score = OffsetMatchScore(downs, timesByLane, offset);
            if (score.Matched > best.Matched || (score.Matched == best.Matched && score.MeanError < best.MeanError))
            {
                bestOffset = offset;
                best = score;
            }
        }

        var refined = bestOffset;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var errors = new List<double>();
            foreach (var press in downs.Take(2048))
            {
                if (!timesByLane.TryGetValue(press.Lane, out var values)) continue;
                var nearest = Nearest(values, press.TimeMs + refined);
                if (nearest is null) continue;
                var error = press.TimeMs + refined - nearest.Value;
                if (error is >= -180 and <= 180) errors.Add(error);
            }
            if (errors.Count < 8) break;
            var mean = errors.Average();
            if (Math.Abs(mean) < .05) break;
            refined -= mean;
        }

        var refinedScore = OffsetMatchScore(downs, timesByLane, refined);
        if (refinedScore.Matched >= best.Matched && refinedScore.MeanError <= best.MeanError + 8)
        {
            bestOffset = refined;
            best = refinedScore;
        }

        var sampleSize = Math.Min(downs.Count, 2048);
        var improvement = Math.Max(4, (int)Math.Floor(sampleSize * .005));
        var improvedMatch = best.Matched >= baseScore.Matched + improvement;
        var improvedPrecision = best.Matched >= Math.Max(1, (int)Math.Floor(baseScore.Matched * .98))
            && best.MeanError + 20 < baseScore.MeanError;
        return Math.Abs(bestOffset) >= 8 && (improvedMatch || improvedPrecision)
            && best.MeanError <= baseScore.MeanError + 8 ? bestOffset : 0;
    }

    private static (int Matched, double MeanError) OffsetMatchScore(
        IReadOnlyList<ReplayInputAction> downs,
        IReadOnlyDictionary<int, List<double>> timesByLane,
        double offset)
    {
        var matched = 0;
        var errorSum = 0d;
        foreach (var press in downs.Take(2048))
        {
            if (!timesByLane.TryGetValue(press.Lane, out var times)) continue;
            var nearest = Nearest(times, press.TimeMs + offset);
            if (nearest is null) continue;
            var error = press.TimeMs + offset - nearest.Value;
            if (error is < -180 or > 180) continue;
            matched++;
            errorSum += Math.Abs(error);
        }
        return (matched, matched == 0 ? double.PositiveInfinity : errorSum / matched);
    }

    private static int LowerBound(IReadOnlyList<double> values, double target)
    {
        var low = 0;
        var high = values.Count;
        while (low < high)
        {
            var middle = (low + high) >> 1;
            if (values[middle] < target) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    private static double? Nearest(IReadOnlyList<double> values, double target)
    {
        if (values.Count == 0) return null;
        var low = LowerBound(values, target);
        var hasNext = low < values.Count;
        var hasPrevious = low > 0;
        if (!hasNext) return hasPrevious ? values[low - 1] : null;
        if (!hasPrevious) return values[low];
        return Math.Abs(values[low] - target) < Math.Abs(values[low - 1] - target) ? values[low] : values[low - 1];
    }

    private static ReplayProfileScore ScoreProfile(
        IReadOnlyList<ReplayChartNote> chart,
        IReadOnlyList<ReplayInputAction> actions,
        Profile profile,
        double calibration)
    {
        var earlyWindow = profile.TimingWindows.Length > 0 ? profile.TimingWindows.Min(window => window.Early) : -profile.DefaultWindow;
        var lateWindow = profile.TimingWindows.Length > 0 ? profile.TimingWindows.Max(window => window.Late) : profile.DefaultWindow;
        var byLane = new Dictionary<int, List<int>>();
        for (var index = 0; index < chart.Count; index++)
        {
            if (!byLane.TryGetValue(chart[index].Lane, out var list)) byLane.Add(chart[index].Lane, list = new());
            list.Add(index);
        }

        var cursors = new Dictionary<int, int>();
        var heads = new Dictionary<int, double>();
        var releases = new Dictionary<int, double>();
        var holds = new Dictionary<int, int>();
        foreach (var action in actions)
        {
            if (!action.IsDown)
            {
                if (!holds.Remove(action.Lane, out var headIndex)) continue;
                var hold = chart[headIndex];
                if (hold.HoldEndMs is null) continue;
                var error = action.TimeMs + calibration - hold.HoldEndMs.Value;
                if (profile.TimingWindows.Any(window => error >= window.Early * 1.5 && error <= window.Late * 1.5)
                    || profile.TimingWindows.Length == 0 && Math.Abs(error) <= profile.DefaultWindow * 1.5)
                    releases[headIndex] = error;
                continue;
            }

            if (!byLane.TryGetValue(action.Lane, out var indices)) continue;
            var cursor = cursors.GetValueOrDefault(action.Lane);
            var pressTime = action.TimeMs + calibration;
            while (cursor < indices.Count && chart[indices[cursor]].TimeMs < pressTime - lateWindow) cursor++;
            if (cursor >= indices.Count) continue;
            var candidate = indices[cursor];
            var headError = pressTime - chart[candidate].TimeMs;
            if (headError < earlyWindow || headError > lateWindow) continue;
            cursors[action.Lane] = cursor + 1;
            heads[candidate] = headError;
            if (chart[candidate].HoldEndMs is not null) holds[action.Lane] = candidate;
        }

        var objects = new List<ScoredObject>();
        foreach (var (note, index) in chart.Select((note, index) => (note, index)))
        {
            var headError = heads.GetValueOrDefault(index, double.NaN);
            var headAccuracy = double.IsNaN(headError) ? 0 : ProfileAccuracy(profile, headError);
            if (note.HoldEndMs is not null && profile.Group == "osu")
            {
                var tailAccuracy = releases.TryGetValue(index, out var releaseError)
                    ? WindowedAccuracy(profile, releaseError, 1.5)
                    : 0;
                objects.Add(new(note.TimeMs, Math.Min(headAccuracy, tailAccuracy)));
            }
            else
            {
                objects.Add(new(note.TimeMs, headAccuracy));
                if (note.HoldEndMs is not null && profile.IncludeTail)
                {
                    var tailAccuracy = releases.TryGetValue(index, out var releaseError)
                        ? ProfileAccuracy(profile, releaseError, 1.5)
                        : 0;
                    objects.Add(new(note.HoldEndMs.Value, tailAccuracy));
                }
            }
        }

        var accuracy = objects.Count == 0 ? 0 : objects.Average(item => item.Accuracy);
        var misses = objects.Count(item => item.Accuracy == 0);
        var headErrors = heads.Values.ToArray();
        return new(
            profile.Group,
            profile.Label,
            accuracy,
            objects.Count,
            misses,
            headErrors.Length == 0 ? 0 : headErrors.Average(),
            profile.WindowLabel,
            BuildProfilePoints(objects));
    }

    private static IReadOnlyList<ReplayProfilePoint> BuildProfilePoints(IReadOnlyList<ScoredObject> objects)
    {
        if (objects.Count == 0) return Array.Empty<ReplayProfilePoint>();
        const int pointCount = 64;
        var ordered = objects.OrderBy(item => item.TimeMs).ToArray();
        var first = ordered[0].TimeMs;
        var last = ordered[^1].TimeMs;
        if (last <= first) return new[] { new ReplayProfilePoint(first, ordered.Average(item => item.Accuracy)) };
        var buckets = new (double Sum, int Count)[pointCount];
        foreach (var item in ordered)
        {
            var index = Math.Min(pointCount - 1, (int)((item.TimeMs - first) / (last - first) * pointCount));
            buckets[index].Sum += item.Accuracy;
            buckets[index].Count++;
        }
        return buckets.Select((bucket, index) => new ReplayProfilePoint(
            first + (last - first) * index / (pointCount - 1),
            bucket.Count == 0 ? 0 : bucket.Sum / bucket.Count)).ToArray();
    }

    private static double ProfileAccuracy(Profile profile, double error, double releaseScale = 1)
    {
        if (profile.Family == ScoreFamily.Windows) return WindowedAccuracy(profile, error, releaseScale);
        return profile.Family switch
        {
            ScoreFamily.StudentT => StudentT(error, profile.DefaultWindow),
            ScoreFamily.Wife => Wife3(error, profile.WifeVersion),
            _ => Quadratic(error, profile.DefaultWindow)
        };
    }

    private static double WindowedAccuracy(Profile profile, double error, double releaseScale)
    {
        if (profile.TimingWindows.Length > 0)
        {
            for (var index = 0; index < profile.TimingWindows.Length; index++)
            {
                var window = profile.TimingWindows[index];
                if (error >= window.Early * releaseScale && error <= window.Late * releaseScale)
                    return profile.Weights.ElementAtOrDefault(index);
            }
            return 0;
        }

        var absolute = Math.Abs(error);
        for (var index = 0; index < profile.Windows.Length; index++)
            if (absolute <= profile.Windows[index] * releaseScale) return profile.Weights.ElementAtOrDefault(index);
        return 0;
    }

    private static double Quadratic(double error, double window)
    {
        var ratio = Math.Abs(error) / window;
        return ratio >= 1 ? 0 : 1 - ratio * ratio;
    }

    private static double StudentT(double error, double window)
    {
        var absolute = Math.Abs(error);
        if (absolute >= window) return 0;
        var scale = 20 * window / 100;
        static double Survival(double value, double scale) => 1 - value / Math.Sqrt(value * value + 2 * scale * scale);
        var atWindow = Survival(window, scale);
        return Math.Clamp((Survival(absolute, scale) - atWindow) / (1 - atWindow), 0, 1);
    }

    private static double Wife3(double error, int version)
    {
        var scale = (10 - Math.Clamp(version, 4, 9)) / 6d;
        var absolute = Math.Abs(error);
        var ridic = 5 * scale;
        var booWindow = 180 * scale;
        var zero = 65 * Math.Pow(scale, .75);
        var deviation = 22.7 * Math.Pow(scale, .75);
        if (absolute <= ridic) return 1;
        if (absolute <= zero) return Math.Max(0, ErfApprox((zero - absolute) / deviation));
        if (absolute <= booWindow) return Math.Max(0, (absolute - zero) * -2.75 / (booWindow - zero));
        return 0;
    }

    private static double ErfApprox(double value)
    {
        var sign = value < 0 ? -1 : 1;
        var x = Math.Abs(value);
        var t = 1 / (1 + .3275911 * x);
        var polynomial = 1 - (((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - .284496736) * t + .254829592) * t * Math.Exp(-x * x));
        return sign * polynomial;
    }

    private static Profile[] DefaultProfiles()
    {
        var profiles = new List<Profile>
        {
            new("Baka", "Baka", ScoreFamily.StudentT, 100, Array.Empty<double>(), Array.Empty<double>(), Array.Empty<(double, double)>(), WindowLabel: "M=100ms"),
            new("Baka", "baka", ScoreFamily.Quadratic, 100, Array.Empty<double>(), Array.Empty<double>(), Array.Empty<(double, double)>(), WindowLabel: "M=100ms"),
            new("Baka", "Baka-e", ScoreFamily.StudentT, 100, Array.Empty<double>(), Array.Empty<double>(), Array.Empty<(double, double)>(), IncludeTail: true, WindowLabel: "M=100ms"),
            new("Baka", "baka-e", ScoreFamily.Quadratic, 100, Array.Empty<double>(), Array.Empty<double>(), Array.Empty<(double, double)>(), IncludeTail: true, WindowLabel: "M=100ms")
        };

        var malody = new[]
        {
            ("Malody/A", new[] { 45d, 90, 126, 150 }),
            ("Malody/B", new[] { 44d, 84, 118, 150 }),
            ("Malody/C", new[] { 36d, 76, 110, 150 }),
            ("Malody/D", new[] { 28d, 68, 102, 150 }),
            ("Malody/E", new[] { 20d, 60, 94, 150 })
        };
        foreach (var (label, windows) in malody)
            profiles.Add(new("Malody", label, ScoreFamily.Windows, windows[^1], windows, new[] { 1d, .75, .4, 0 }, Array.Empty<(double, double)>(), WindowLabel: $"{windows[^1]:0}ms"));

        var quaver = new (string Name, double[] Windows)[]
        {
            ("peaceful", new[] { 23d, 57, 101, 141, 169, 218 }),
            ("lenient", new[] { 21d, 52, 91, 128, 153, 198 }),
            ("standard", new[] { 18d, 43, 76, 106, 127, 164 }),
            ("tough", new[] { 14d, 35, 62, 87, 127, 164 }),
            ("chill", new[] { 19d, 47, 83, 116, 139, 180 }),
            ("strict", new[] { 16d, 39, 69, 96, 127, 164 }),
            ("extreme", new[] { 13d, 32, 57, 79, 127, 164 }),
            ("impossible", new[] { 8d, 20, 35, 49, 127, 164 })
        };
        foreach (var (name, windows) in quaver)
            profiles.Add(new("Quaver", $"Quaver/{name}", ScoreFamily.Windows, windows[^1], windows, new[] { 1d, .9825, .65, .25, 0, 0 }, Array.Empty<(double, double)>(), WindowLabel: $"{windows[^1]:0}ms"));

        profiles.Add(MakeScProfile(4));
        profiles.Add(MakeWifeProfile(4));
        profiles.Add(MakeOsuProfile(false, 8));
        profiles.Add(MakeOsuProfile(true, 8));
        return profiles.ToArray();
    }

    private static Profile MakeScProfile(int judge)
    {
        var perfect = judge == 9 ? 9 : 45 * (10 - judge) / 6d;
        var missPenalty = judge switch
        {
            9 => -20,
            8 => -10,
            7 => -4.7,
            6 => -2.6,
            5 => -1.6,
            4 => -1,
            3 => -.6,
            _ => -.4
        };
        var missWindow = Math.Max(180, perfect * 4);
        return new("SC", $"SC J{judge}", ScoreFamily.Windows, missWindow, Array.Empty<double>(),
            new[] { 1d, .9, .5, 0, missPenalty }, new[]
            {
                (-perfect * .5, perfect * .5), (-perfect, perfect), (-perfect * 2, perfect * 2),
                (-perfect * 3, perfect * 3), (-missWindow, missWindow)
            }, WindowLabel: $"J{judge}");
    }

    private static Profile MakeWifeProfile(int version) =>
        new("Wife3", version == 9 ? "Wife3 JUSTICE" : $"Wife3 J{version}", ScoreFamily.Wife, 180,
            Array.Empty<double>(), Array.Empty<double>(), Array.Empty<(double, double)>(), WifeVersion: version, WindowLabel: $"J{version}");

    private static Profile MakeOsuProfile(bool scoreV2, double od)
    {
        var w300 = Math.Max(1, 64.5 - 3 * od);
        var w200 = Math.Max(1, 97.5 - 3 * od);
        var w100 = Math.Max(1, 127.5 - 3 * od);
        var w50 = Math.Max(1, 151.5 - 3 * od);
        var w0 = Math.Max(1, 188.5 - 3 * od);
        var w320 = Math.Max(1, od >= 5 ? 24.9 - 1.1 * od : 22.4 - .6 * od);
        var timing = scoreV2
            ? new[] { (-w320, w320), (-w300, w300), (-w200, w200), (-w100, Math.Max(1, w100 - 1)), (-w50, Math.Max(1, w50 - 25)), (-w0, Math.Max(1, w0 - 62)) }
            : new[] { (-16.5, 16.5), (-w300, w300), (-w200, w200), (-w100, Math.Max(1, w100 - 1)), (-w50, Math.Max(1, w50 - 25)), (-w0, Math.Max(1, w0 - 62)) };
        var weights = scoreV2
            ? new[] { 1d, .9836065574, .65573770492, .32786885246, .16393442623, 0 }
            : new[] { 1d, 1, .6666666667, .3333333333, .1666666667, 0 };
        return new(scoreV2 ? "osu-sv2" : "osu", scoreV2 ? "osu!standard / ScoreV2" : "osu!standard", ScoreFamily.Windows, w0,
            Array.Empty<double>(), weights, timing, IncludeTail: scoreV2, WindowLabel: $"OD{od:0}");
    }
}
