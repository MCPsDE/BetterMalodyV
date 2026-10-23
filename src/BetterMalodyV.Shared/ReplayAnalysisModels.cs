namespace BetterMalodyV.Shared;

public sealed record ReplayChartNote(double TimeMs, int Lane, double? HoldEndMs = null);

public sealed record ReplayInputAction(double TimeMs, int Lane, bool IsDown);

public sealed record ReplayHitEvent(double TimeMs, int Lane, int TimingFlags);

public sealed record ReplayJudgementFlags(int Best, int Cool, int Good, int Bad, int Miss, int Early, int Late);

public sealed record ReplayJudgementTally(int Best, int Cool, int Good, int Bad, int Miss, int Early, int Late)
{
    public int Total => Best + Cool + Good + Bad + Miss;
}

public sealed record ReplayValueStats(
    int Count,
    double Total,
    double Average,
    double AverageAbsolute,
    double? AverageEarly,
    double? AverageLate,
    double Median,
    double StandardDeviation,
    double? Minimum,
    double? Maximum);

public sealed record ReplayHistogram(IReadOnlyList<string> Labels, IReadOnlyList<int> Counts);

public sealed record ReplayPressStats(
    int PressCount,
    int CompletedCount,
    int UnmatchedDownCount,
    int UnmatchedUpCount,
    ReplayHistogram DurationDistribution,
    ReplayHistogram IntervalDistribution);

public sealed record ReplayTrackAnalysis(
    int Lane,
    int NoteCount,
    ReplayValueStats Offsets,
    ReplayHistogram OffsetDistribution,
    ReplayPressStats Presses);

public sealed record ReplayProfilePoint(double TimeMs, double Accuracy);

public sealed record ReplayProfileScore(
    string Group,
    string Label,
    double Accuracy,
    int Objects,
    int Misses,
    double MeanError,
    string WindowLabel,
    IReadOnlyList<ReplayProfilePoint> Points);

public sealed record ReplayAnalysisResult(
    int ChartNoteCount,
    int MatchedCount,
    int UnplayedCount,
    int InputCount,
    double CalibrationOffsetMs,
    ReplayValueStats Offsets,
    ReplayHistogram OffsetDistribution,
    ReplayPressStats Presses,
    ReplayJudgementTally Judgements,
    IReadOnlyList<ReplayTrackAnalysis> Tracks,
    IReadOnlyList<ReplayProfileScore> Profiles,
    IReadOnlyList<string> Warnings);

public sealed record ReplayAnalysisSession(
    string Title,
    string Artist,
    string Difficulty,
    string ReplayFileName,
    string Mode,
    uint SavedScore,
    uint SavedCombo,
    double SavedAccuracy,
    string SavedRank,
    ReplayAnalysisResult Analysis);
