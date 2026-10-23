using BetterMalodyV.Shared;

internal static class ReplayAnalysisTests
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new Exception("Replay analysis: " + label);
            checks++;
        }

        var chart = new List<ReplayChartNote>();
        var actions = new List<ReplayInputAction>();
        var hits = new List<ReplayHitEvent>();
        for (var index = 0; index < 12; index++)
        {
            var lane = index % 2;
            var time = 60000 + index * 500;
            chart.Add(new(time, lane));
            actions.Add(new(time - 12000, lane, true));
            actions.Add(new(time - 11920, lane, false));
            hits.Add(new(time, lane, ReplayAnalysisEngine.GameJudgementFlags.Best));
        }

        var calibrated = ReplayAnalysisEngine.Analyze(chart, actions, hits, 2);
        Check(Math.Abs(calibrated.CalibrationOffsetMs - 12000) < .01, "Detects replay/chart start offset");
        Check(calibrated.MatchedCount == 12 && calibrated.UnplayedCount == 0, "Matches each lane note once");
        Check(Math.Abs(calibrated.Offsets.Total) < .01 && Math.Abs(calibrated.Offsets.Average) < .01, "Calibrated offsets are centered");
        Check(calibrated.Tracks.Count == 2 && calibrated.Tracks.All(track => track.Offsets.Count == 6), "Builds separate track statistics");
        Check(calibrated.Presses.CompletedCount == 12 && calibrated.Presses.UnmatchedDownCount == 0, "Pairs press and release events");
        Check(calibrated.Judgements.Best == 12 && calibrated.Judgements.Total == 12, "Counts native judgement flags");
        Check(calibrated.Profiles.Count == 21, "Provides the default CVT profile set");
        Check(calibrated.Profiles.Any(profile => profile.Label == "Malody/E"), "Includes strict Malody profile");
        Check(calibrated.Profiles.All(profile => profile.Points.Sum(point => point.SampleCount) == profile.Objects), "CVT timeline preserves every scored object");
        Check(calibrated.OffsetDistribution.Bins.Sum(bin => bin.Count) == calibrated.MatchedCount, "Offset histogram accounts for every matched hit");

        var small = ReplayAnalysisEngine.Analyze(
            new[] { new ReplayChartNote(1000, 0), new ReplayChartNote(2000, 0) },
            new[]
            {
                new ReplayInputAction(1005, 0, true), new ReplayInputAction(1025, 0, false),
                new ReplayInputAction(1990, 0, true), new ReplayInputAction(2040, 0, false)
            },
            new[] { new ReplayHitEvent(1005, 0, ReplayAnalysisEngine.GameJudgementFlags.Best | ReplayAnalysisEngine.GameJudgementFlags.Late) },
            1);
        Check(small.CalibrationOffsetMs == 0, "Keeps calibration disabled for short samples");
        Check(Math.Abs(small.Offsets.Total + 5) < .01, "Preserves signed total timing error");
        Check(Math.Abs(small.Offsets.AverageLate!.Value - 5) < .01, "Calculates mean positive timing error");
        Check(Math.Abs(small.Offsets.AverageEarly!.Value + 10) < .01, "Calculates mean negative timing error");
        Check(small.Judgements.Best == 1 && small.Judgements.Late == 1, "Separates timing flags from judgement counts");
        Check(small.Presses.DurationDistribution.Bins.Sum(bin => bin.Count) == 2, "Bins completed press durations");
        Check(small.Presses.DurationDistribution.Bins.Single(bin => bin.Start == 20).Count == 1
            && small.Presses.DurationDistribution.Bins.Single(bin => bin.Start == 40).Count == 1,
            "Duration histogram retains unequal numeric bin boundaries");
        Check(small.Presses.IntervalDistribution.Bins.Single(bin => bin.Start == 500).Count == 1,
            "Press interval is assigned to its real 500-1000 ms range");
        var hold = ReplayAnalysisEngine.Analyze(
            new[] { new ReplayChartNote(1000, 0, 2000) },
            new[] { new ReplayInputAction(1000, 0, true), new ReplayInputAction(2110, 0, false) },
            Array.Empty<ReplayHitEvent>(),
            1);
        var baka = hold.Profiles.Single(profile => profile.Label == "Baka");
        var bakaTail = hold.Profiles.Single(profile => profile.Label == "Baka-e");
        Check(baka.Objects == 1 && Math.Abs(baka.Accuracy - 1) < .001, "Baka excludes hold-tail score objects");
        Check(bakaTail.Objects == 2 && Math.Abs(bakaTail.Accuracy - .5) < .001, "Baka-e scores a separate hold tail");

        var sparse = ReplayAnalysisEngine.Analyze(
            new[] { new ReplayChartNote(0, 0), new ReplayChartNote(10, 0), new ReplayChartNote(10000, 0) },
            new[]
            {
                new ReplayInputAction(0, 0, true), new ReplayInputAction(1, 0, false),
                new ReplayInputAction(10, 0, true), new ReplayInputAction(11, 0, false),
                new ReplayInputAction(10000, 0, true), new ReplayInputAction(10001, 0, false)
            },
            Array.Empty<ReplayHitEvent>(),
            1);
        var sparseTimeline = sparse.Profiles.Single(profile => profile.Label == "baka");
        Check(sparseTimeline.Points.Count == 2 && sparseTimeline.Points.Sum(point => point.SampleCount) == 3,
            "CVT timeline omits empty time buckets without dropping notes");
        Check(sparseTimeline.Points[0].EndTimeMs < sparseTimeline.Points[1].StartTimeMs
            && sparseTimeline.ChartStartMs == 0 && sparseTimeline.ChartEndMs == 10000,
            "CVT points retain actual chart time gaps");

        return checks;
    }
}
