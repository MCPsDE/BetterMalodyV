using BetterMalodyV.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BetterMalodyV.Plugin;

internal static class ReplayAnalysisView
{
    private static readonly Color CanvasColor = Hex("0b0c10");
    private static readonly Color SurfaceColor = Hex("14161c");
    private static readonly Color RaisedColor = Hex("1b1e26");
    private static readonly Color HoverColor = Hex("232731");
    private static readonly Color InkColor = Hex("f5f7fa");
    private static readonly Color MutedColor = Hex("a8adb8");
    private static readonly Color SubtleColor = Hex("747b88");
    private static readonly Color BorderColor = Hex("2b2f39");
    private static readonly Color CyanColor = Hex("35cfff");
    private static readonly Color PinkColor = Hex("ff4f8b");
    private static readonly Color MissColor = Hex("f05d6d");

    private static GameObject? root;
    private static TMP_Text? textTemplate;
    private static bool chinese;
    private static ScrollRect? scroll;
    private static RectTransform? viewport;
    private static readonly List<RectTransform> pages = new();
    private static readonly List<Button> tabs = new();
    private static readonly List<PendingPlot> pendingPlots = new();
    private static readonly List<Texture2D> plotTextures = new();

    internal static bool IsOpen => root;
#if BETTER_MALODYV_VERIFY
    internal static Camera? TestCamera;
    internal static GameObject? TestRoot => root;
    internal static IReadOnlyList<ReplayPlotDefinition> TestPlots => pendingPlots.Select(plot => plot.Definition).ToArray();
#endif

    internal static void Open(TMP_Text source, ReplayAnalysisSession session)
    {
        Close();
        textTemplate = source;
        chinese = source.font && source.font.HasCharacter('回', true, true);
        pages.Clear();
        tabs.Clear();

        root = NewRect("BetterMalodyV.ReplayAnalysis");
        root.AddComponent<Canvas>();
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760;
#if BETTER_MALODYV_VERIFY
        if (TestCamera)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = TestCamera;
            canvas.planeDistance = 1;
        }
#endif
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440, 900);
        scaler.matchWidthOrHeight = .5f;
        Stretch(root.GetComponent<RectTransform>());

        var background = Image(root.transform, "Background", CanvasColor);
        Stretch(background.rectTransform);
        BuildHeader(root.transform, session);
        BuildTabs(root.transform);
        BuildScroll(root.transform);
        BuildOverview(session);
        BuildTracks(session);
        BuildPresses(session);
        BuildProfiles(session);
        SelectPage(0);
    }

    internal static void Close()
    {
        if (root) UnityEngine.Object.Destroy(root);
        foreach (var texture in plotTextures)
            if (texture) UnityEngine.Object.Destroy(texture);
        plotTextures.Clear();
        pendingPlots.Clear();
        root = null;
        textTemplate = null;
        scroll = null;
        viewport = null;
        pages.Clear();
        tabs.Clear();
    }

    private static void BuildHeader(Transform parent, ReplayAnalysisSession session)
    {
        var header = Panel(parent, "Header", SurfaceColor);
        var rect = header.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(0, 76);
        rect.anchoredPosition = Vector2.zero;
        var rule = Image(header.transform, "Rule", BorderColor);
        var ruleRect = rule.rectTransform;
        ruleRect.anchorMin = new Vector2(0, 0);
        ruleRect.anchorMax = new Vector2(1, 0);
        ruleRect.pivot = new Vector2(.5f, 0);
        ruleRect.sizeDelta = new Vector2(0, 1);
        ruleRect.anchoredPosition = Vector2.zero;

        var backLabel = chinese ? "← 返回" : "← Back";
        var back = Button(header.transform, backLabel, "Back", RaisedColor, InkColor, 124, 40);
        SetTopLeft(back.GetComponent<RectTransform>(), 24, 18, 124, 40);
        back.onClick.AddListener((UnityAction)Close);

        AddAnchoredText(header.transform, chinese ? "回放分析" : "Replay analysis", 24, FontStyles.Bold, InkColor,
            new Vector2(168, -12), new Vector2(0, 1), new Vector2(0, 1), new Vector2(400, 34));
        var metadata = string.Join("  ·  ", new[] { session.Mode, session.Difficulty }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        AddAnchoredText(header.transform, session.Title + (metadata.Length == 0 ? string.Empty : "  ·  " + metadata),
            13, FontStyles.Normal, MutedColor, new Vector2(170, -46), new Vector2(0, 1), new Vector2(1, 1), new Vector2(-194, 20), true);
    }

    private static void BuildTabs(Transform parent)
    {
        var row = Panel(parent, "Tabs", CanvasColor);
        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(0, 54);
        rect.anchoredPosition = new Vector2(0, -76);
        var labels = chinese ? new[] { "总览", "轨道", "按压", "判定换算" } : new[] { "Overview", "Tracks", "Presses", "Judgements" };
        var widths = chinese ? new[] { 96f, 96f, 96f, 120f } : new[] { 116f, 100f, 108f, 142f };
        var x = 24f;
        for (var index = 0; index < labels.Length; index++)
        {
            var tab = Button(row.transform, labels[index], "Tab " + labels[index], RaisedColor, MutedColor, widths[index], 36);
            var tabRect = tab.GetComponent<RectTransform>();
            SetTopLeft(tabRect, x, 8, widths[index], 36);
            var pageIndex = index;
            tab.onClick.AddListener((UnityAction)(() => SelectPage(pageIndex)));
            tabs.Add(tab);
            x += widths[index] + 8;
        }
    }

    private static void BuildScroll(Transform parent)
    {
        var scrollObject = NewRect("PageScroll", parent);
        scrollObject.AddComponent<Image>();
        scrollObject.AddComponent<Mask>();
        scrollObject.AddComponent<ScrollRect>();
        var scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = new Vector2(0, -130);
        scrollObject.GetComponent<Image>().color = CanvasColor;
        scrollObject.GetComponent<Mask>().showMaskGraphic = false;
        scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50;

        var viewportObject = NewRect("Viewport", scrollObject.transform);
        viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport);
        scroll.viewport = viewport;
    }

    private static void BuildOverview(ReplayAnalysisSession session)
    {
        var analysis = session.Analysis;
        var page = NewPage("OverviewPage");
        AddText(page, session.Title, 23, FontStyles.Bold, InkColor, 36, true);
        AddText(page, string.Join("  ·  ", new[] { session.Artist, session.Difficulty, session.ReplayFileName }
            .Where(value => !string.IsNullOrWhiteSpace(value))), 13, FontStyles.Normal, MutedColor, 38, true);
        var summary = new (string Label, string Value)[]
        {
            (Text("记录成绩", "Saved score"), session.SavedScore.ToString("N0")),
            (Text("准确率", "Accuracy"), $"{session.SavedAccuracy:0.00}%"),
            (Text("最大连击", "Max combo"), session.SavedCombo.ToString("N0")),
            (Text("实际判定数", "Judgements"), analysis.Judgements.Total.ToString("N0")),
            (Text("谱面音符", "Chart notes"), analysis.ChartNoteCount.ToString("N0")),
            (Text("已匹配", "Matched"), analysis.MatchedCount.ToString("N0")),
            (Text("未匹配", "Unmatched"), analysis.UnplayedCount.ToString("N0")),
            (Text("自动校准", "Calibration"), Signed(analysis.CalibrationOffsetMs))
        };
        AddSection(page, Text("成绩概览", "Run summary"));
        AddStatGrid(page, summary);

        AddSection(page, Text("打击偏移", "Hit timing"));
        AddStatGrid(page, new[]
        {
            (Text("总偏移", "Total offset"), Signed(analysis.Offsets.Total)),
            (Text("平均偏移", "Mean offset"), Signed(analysis.Offsets.Average)),
            (Text("平均正偏移", "Mean late"), OptionalSigned(analysis.Offsets.AverageLate)),
            (Text("平均负偏移", "Mean early"), OptionalSigned(analysis.Offsets.AverageEarly)),
            (Text("平均绝对偏移", "Mean absolute"), Ms(analysis.Offsets.AverageAbsolute)),
            (Text("中位数", "Median"), Signed(analysis.Offsets.Median)),
            (Text("标准差", "Std. deviation"), Ms(analysis.Offsets.StandardDeviation)),
            (Text("命中数", "Matched hits"), analysis.Offsets.Count.ToString("N0"))
        });
        AddHistogramSection(page, Text("整体误差分布", "Overall offset distribution"), analysis.OffsetDistribution, true, "OverallOffset");

        AddSection(page, Text("原始判定", "Recorded judgements"));
        AddStatGrid(page, new[]
        {
            ("Best", analysis.Judgements.Best.ToString("N0")),
            ("Cool", analysis.Judgements.Cool.ToString("N0")),
            ("Good", analysis.Judgements.Good.ToString("N0")),
            ("Bad", analysis.Judgements.Bad.ToString("N0")),
            ("Miss", analysis.Judgements.Miss.ToString("N0")),
            (Text("Early", "Early"), analysis.Judgements.Early.ToString("N0")),
            (Text("Late", "Late"), analysis.Judgements.Late.ToString("N0")),
            (Text("判定等级", "Rank"), session.SavedRank)
        });
        AddWarnings(page, analysis.Warnings);
    }

    private static void BuildTracks(ReplayAnalysisSession session)
    {
        var page = NewPage("TracksPage");
        AddSection(page, Text("逐轨偏移", "Offset by track"));
        AddText(page, Text("正值表示偏晚，负值表示偏早；总偏移为逐击偏移之和。", "Positive values are late, negative values are early; total offset is the sum of hit errors."), 14, FontStyles.Normal, MutedColor, 42, true);
        var columns = Screen.width < 1500 ? 1 : 2;
        var grid = Grid(page, columns, columns == 1 ? 1400 : 680, 510, 16);
        foreach (var track in session.Analysis.Tracks)
        {
            var card = Panel(grid.transform, "Track " + (track.Lane + 1), SurfaceColor);
            var layout = Vertical(card.transform, 14, 14, 14, 14, 8);
            AddText(layout.transform, Text("轨道", "Track") + " " + (track.Lane + 1), 18, FontStyles.Bold, InkColor, 28);
            AddStatGrid(layout.transform, new[]
            {
                (Text("总偏移", "Total"), Signed(track.Offsets.Total)),
                (Text("平均偏移", "Mean"), Signed(track.Offsets.Average)),
                (Text("平均正偏移", "Mean late"), OptionalSigned(track.Offsets.AverageLate)),
                (Text("平均负偏移", "Mean early"), OptionalSigned(track.Offsets.AverageEarly)),
                (Text("匹配音符", "Matched"), track.Offsets.Count.ToString("N0")),
                (Text("谱面音符", "Chart notes"), track.NoteCount.ToString("N0"))
            }, 3, 200, 58);
            AddCompactHistogram(layout.transform, Text("轨道误差分布", "Track offset distribution"), track.OffsetDistribution, true,
                "TrackOffset-" + track.Lane);
        }
    }

    private static void BuildPresses(ReplayAnalysisSession session)
    {
        var analysis = session.Analysis;
        var page = NewPage("PressesPage");
        AddSection(page, Text("整体按压", "Press summary"));
        AddStatGrid(page, new[]
        {
            (Text("按下次数", "Presses"), analysis.Presses.PressCount.ToString("N0")),
            (Text("完整按压", "Completed"), analysis.Presses.CompletedCount.ToString("N0")),
            (Text("未抬起", "Still held"), analysis.Presses.UnmatchedDownCount.ToString("N0")),
            (Text("无对应按下的抬起", "Unmatched releases"), analysis.Presses.UnmatchedUpCount.ToString("N0"))
        });
        AddHistogramSection(page, Text("按压时长分布", "Press duration distribution"), analysis.Presses.DurationDistribution, false, "PressDuration");
        AddHistogramSection(page, Text("同轨按压间隔", "Interval between presses"), analysis.Presses.IntervalDistribution, false, "PressInterval");

        AddSection(page, Text("逐轨按压", "Presses by track"));
        var columns = Screen.width < 1500 ? 1 : 2;
        var grid = Grid(page.transform, columns, columns == 1 ? 1400 : 680, 560, 16);
        foreach (var track in analysis.Tracks)
        {
            var card = Panel(grid.transform, "Press track " + (track.Lane + 1), SurfaceColor);
            var layout = Vertical(card.transform, 14, 14, 14, 14, 8);
            AddText(layout.transform, Text("轨道", "Track") + " " + (track.Lane + 1), 18, FontStyles.Bold, InkColor, 28);
            AddStatGrid(layout.transform, new[]
            {
                (Text("按下次数", "Presses"), track.Presses.PressCount.ToString("N0")),
                (Text("有效时长", "Durations"), track.Presses.CompletedCount.ToString("N0")),
                (Text("未抬起", "Still held"), track.Presses.UnmatchedDownCount.ToString("N0")),
                (Text("未配对抬起", "Unmatched up"), track.Presses.UnmatchedUpCount.ToString("N0"))
            }, 2, 210, 58);
            AddCompactHistogram(layout.transform, Text("按压时长", "Duration"), track.Presses.DurationDistribution, false,
                "TrackDuration-" + track.Lane);
            AddCompactHistogram(layout.transform, Text("按压间隔", "Interval"), track.Presses.IntervalDistribution, false,
                "TrackInterval-" + track.Lane);
        }
    }

    private static void BuildProfiles(ReplayAnalysisSession session)
    {
        var page = NewPage("JudgementsPage");
        AddText(page, Text("CVT 判定换算", "CVT judgement conversion"), 22, FontStyles.Bold, InkColor, 34);
        AddText(page, Text("按 MalodySite 的判定窗口、自动校准与准确率公式估算；仅作回放比较，不代表官方结算分。", "Estimated with MalodySite judgement windows, calibration and accuracy formulas; this is a replay comparison, not an official result."), 14, FontStyles.Normal, MutedColor, 46, true);
        AddStatGrid(page, new[]
        {
            (Text("匹配音符", "Matched notes"), session.Analysis.MatchedCount.ToString("N0")),
            (Text("校准偏移", "Calibration"), Signed(session.Analysis.CalibrationOffsetMs)),
            (Text("总音符", "Chart notes"), session.Analysis.ChartNoteCount.ToString("N0")),
            (Text("估算档位", "Profiles"), session.Analysis.Profiles.Count.ToString("N0"))
        });

        foreach (var group in session.Analysis.Profiles.GroupBy(profile => profile.Group))
        {
            AddSection(page, group.Key);
            var columns = Screen.width < 1500 ? 1 : 2;
            var grid = Grid(page.transform, columns, columns == 1 ? 1400 : 680, 334, 12);
            foreach (var profile in group)
            {
                var card = Panel(grid.transform, profile.Label, SurfaceColor);
                var layout = Vertical(card.transform, 14, 14, 14, 14, 6);
                var head = Horizontal(layout.transform, 12, 0, 0, 0, 5);
                AddText(head.transform, profile.Label, 16, FontStyles.Bold, InkColor, 24, false, 1);
                AddText(head.transform, $"{profile.Accuracy * 100:0.00}%", 18, FontStyles.Bold, CyanColor, 24, false, 0);
                AddText(layout.transform, string.Join("  ·  ", new[]
                {
                    profile.WindowLabel,
                    Text("漏判", "Misses") + " " + profile.Misses.ToString("N0"),
                    Text("平均偏移", "Mean error") + " " + Signed(profile.MeanError)
                }), 12, FontStyles.Normal, MutedColor, 24, true);
                AddTimeline(layout.transform, profile, 220);
            }
        }
        if (session.Analysis.Profiles.Count == 0)
            AddWarnings(page, new[] { Text("该玩法没有可用的判定换算档位。", "No judgement conversion profiles are available for this mode.") });
    }

    private static RectTransform NewPage(string name)
    {
        var pageObject = NewRect(name, viewport);
        pageObject.AddComponent<VerticalLayoutGroup>();
        pageObject.AddComponent<ContentSizeFitter>();
        var rect = pageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        var layout = pageObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = Padding(24, 24, 22, 28);
        layout.spacing = 14;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        pageObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var element = pageObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1;
        pages.Add(rect);
        return rect;
    }

    private static void SelectPage(int index)
    {
        if (index < 0 || index >= pages.Count) return;
        for (var i = 0; i < pages.Count; i++) pages[i].gameObject.SetActive(i == index);
        for (var i = 0; i < tabs.Count; i++)
        {
            var selected = i == index;
            var image = tabs[i].targetGraphic as Image;
            if (image) image!.color = selected ? HoverColor : RaisedColor;
            var label = tabs[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label) label!.color = selected ? CyanColor : MutedColor;
        }
        if (scroll) scroll!.content = pages[index];
        if (scroll) scroll!.verticalNormalizedPosition = 1;
        Canvas.ForceUpdateCanvases();
        RenderPendingPlots(pages[index]);
    }

    private static void AddSection(Transform parent, string title)
    {
        var block = Block(parent, "Section", 14, 14, 0, 0, 7);
        AddText(block.transform, title, 19, FontStyles.Bold, InkColor, 30);
    }

    private static void AddStatGrid(Transform parent, IReadOnlyList<(string Label, string Value)> values, int columns = 4, float cellWidth = 350, float cellHeight = 72)
    {
        var rows = (int)Math.Ceiling(values.Count / (double)columns);
        var grid = Grid(parent, columns, cellWidth, cellHeight, 10);
        grid.GetComponent<LayoutElement>().preferredHeight = rows * cellHeight + Math.Max(0, rows - 1) * 10;
        foreach (var (label, value) in values)
        {
            var tile = Panel(grid.transform, label, RaisedColor);
            tile.GetComponent<LayoutElement>().preferredHeight = cellHeight;
            var layout = Vertical(tile.transform, 12, 10, 12, 10, 4);
            AddText(layout.transform, label, 14, FontStyles.Normal, MutedColor, 20, false);
            AddText(layout.transform, value, 18, FontStyles.Bold, InkColor, 25, false);
        }
    }

    private static void AddHistogramSection(Transform parent, string title, ReplayHistogram histogram, bool signed, string plotId)
    {
        var block = Block(parent, title, 14, 14, 14, 14, 7);
        AddText(block.transform, title, 16, FontStyles.Bold, InkColor, 25);
        AddHistogram(block.transform, histogram, signed, 240, plotId);
    }

    private static void AddCompactHistogram(Transform parent, string title, ReplayHistogram histogram, bool signed, string plotId)
    {
        AddText(parent, title, 13, FontStyles.Normal, MutedColor, 20);
        AddHistogram(parent, histogram, signed, 156, plotId);
    }

    private static void AddHistogram(Transform parent, ReplayHistogram histogram, bool signed, float height, string plotId)
    {
        var graph = NewRect("Histogram", parent);
        graph.AddComponent<Image>();
        graph.GetComponent<Image>().color = RaisedColor;
        var rect = graph.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, height);
        var element = graph.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        if (histogram.Bins.Count == 0) return;

        var binEnds = Enumerable.Range(0, histogram.Bins.Count)
            .Select(index => HistogramBinEnd(histogram.Bins, index)).ToArray();
        var densities = histogram.Bins.Select((bin, index) =>
        {
            var width = binEnds[index] - bin.Start;
            return width > 0 ? bin.Count / width : 0;
        }).ToArray();
        var maxDensity = densities.Max();
        var axisStart = histogram.Bins[0].Start;
        var axisEnd = binEnds[^1];
        var axisSpan = axisEnd - axisStart;
        var plotPoints = histogram.Bins.Select((bin, index) => new ReplayPlotPoint(
            (bin.Start + binEnds[index]) / 2,
            maxDensity > 0 ? densities[index] / maxDensity : 0,
            signed && (bin.Start + binEnds[index]) / 2 < 0)).ToArray();
        QueuePlot(graph.transform, graph.GetComponent<RectTransform>(), plotId, plotPoints, axisStart, axisEnd,
            true, signed, CyanColor, PinkColor, MissColor, BorderColor, Hex("566171"));
        AddPercentLabels(graph.transform, height, height >= 128);

        AddHistogramAxis(parent, histogram, signed, axisStart, axisEnd);
    }

    private static double HistogramBinEnd(IReadOnlyList<ReplayHistogramBin> bins, int index)
    {
        var bin = bins[index];
        if (double.IsFinite(bin.End)) return bin.End;
        var previous = index > 0 ? bins[index - 1] : bin;
        var width = double.IsFinite(previous.End) ? previous.End - previous.Start : 1;
        return bin.Start + Math.Max(1, width);
    }

    private static void AddHistogramAxis(Transform parent, ReplayHistogram histogram, bool signed, double start, double end)
    {
        var axis = NewRect("HistogramAxis", parent);
        var rect = axis.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(0, 18);
        var element = axis.AddComponent<LayoutElement>();
        element.preferredHeight = 18;
        element.minHeight = 18;

        if (signed)
        {
            for (var index = 0; index <= 4; index++)
            {
                var value = start + (end - start) * index / 4;
                var label = $"{(value > 0 ? "+" : string.Empty)}{value:0} {histogram.Unit}";
                AddAxisLabel(axis.transform, label, (float)index / 4, 80,
                    index == 0 ? TextAlignmentOptions.Left : index == 4 ? TextAlignmentOptions.Right : TextAlignmentOptions.Center);
            }
            return;
        }

        var overflow = histogram.Bins[^1];
        if (!double.IsPositiveInfinity(overflow.End))
        {
            for (var index = 0; index <= 4; index++)
            {
                var value = start + (end - start) * index / 4;
                AddAxisLabel(axis.transform, $"{value:0} {histogram.Unit}", (float)index / 4, 88,
                    index == 0 ? TextAlignmentOptions.Left : index == 4 ? TextAlignmentOptions.Right : TextAlignmentOptions.Center);
            }
            return;
        }

        var threshold = overflow.Start;
        var midpoint = start + (threshold - start) / 2;
        AddAxisLabel(axis.transform, $"{start:0} {histogram.Unit}", 0, 82, TextAlignmentOptions.Left);
        AddAxisLabel(axis.transform, $"{midpoint:0} {histogram.Unit}", (float)((midpoint - start) / (end - start)), 88,
            TextAlignmentOptions.Center);
        AddAxisLabel(axis.transform, $"{threshold:0} {histogram.Unit}", (float)((threshold - start) / (end - start)), 82,
            TextAlignmentOptions.Center);
        AddAxisLabel(axis.transform, $"{threshold:0}+ {histogram.Unit}", 1, 88, TextAlignmentOptions.Right);
    }

    private static void AddAxisLabel(Transform parent, string label, float x, float width, TextAlignmentOptions alignment)
    {
        var offset = ReplayPlotTexture.PlotLeft * (1 - x) - ReplayPlotTexture.PlotRight * x;
        var text = AddAnchoredText(parent, label, 10, FontStyles.Normal, SubtleColor,
            new Vector2(offset, 0), new Vector2(x, 1), new Vector2(x, 1), new Vector2(width, 18));
        text.rectTransform.pivot = new Vector2(x <= 0 ? 0 : x >= 1 ? 1 : .5f, 1);
        text.alignment = alignment;
    }

    private static void QueuePlot(Transform parent, RectTransform container, string id,
        IReadOnlyList<ReplayPlotPoint> points, double minimumX, double maximumX, bool connectPoints,
        bool signed, Color positive, Color early, Color miss, Color grid, Color line)
    {
        var textureObject = NewRect("PlotTexture", parent);
        Stretch(textureObject.GetComponent<RectTransform>());
        var image = textureObject.AddComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        pendingPlots.Add(new(container, image,
            new ReplayPlotDefinition(id, points.ToArray(), minimumX, maximumX, connectPoints, signed),
            positive, early, miss, grid, line));
    }

    private static void RenderPendingPlots(RectTransform page)
    {
        foreach (var plot in pendingPlots)
        {
            if (plot.Rendered || !plot.Container || !plot.Container.IsChildOf(page)) continue;
            var width = Mathf.RoundToInt(plot.Container.rect.width);
            var height = Mathf.RoundToInt(plot.Container.rect.height);
            if (width <= 0 || height <= 0) continue;
            var texture = ReplayPlotTexture.Create(width, height, plot.Definition.Points,
                plot.Definition.MinimumX, plot.Definition.MaximumX, plot.Definition.ConnectPoints,
                plot.Positive, plot.Early, plot.Miss, plot.Grid, plot.Line);
            plot.Image.texture = texture;
            plot.Image.SetAllDirty();
            plotTextures.Add(texture);
            plot.Rendered = true;
        }
    }

    private static void AddPercentLabels(Transform parent, float height, bool allTicks)
    {
        var steps = allTicks ? new[] { 0, 1, 2, 3, 4 } : new[] { 0, 4 };
        var plotHeight = height - ReplayPlotTexture.PlotTop - ReplayPlotTexture.PlotBottom;
        foreach (var step in steps)
        {
            var y = ReplayPlotTexture.PlotTop + plotHeight * step / 4;
            var text = AddAnchoredText(parent, $"{100 - step * 25}%", 10, FontStyles.Normal, SubtleColor,
                new Vector2(2, -(y - 6)), Vector2.up, Vector2.up, new Vector2(36, 12));
            text.alignment = TextAlignmentOptions.Right;
        }
    }

    private static void AddTimeline(Transform parent, ReplayProfileScore profile, float height)
    {
        var graph = NewRect("AccuracyTimeline", parent);
        graph.AddComponent<Image>();
        graph.GetComponent<Image>().color = RaisedColor;
        var rect = graph.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, height);
        var element = graph.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        var maxTime = profile.Points.Count == 0 ? 1 : Math.Max(1, profile.Points[^1].TimeMs);
        var points = profile.Points.Select(point => new ReplayPlotPoint(
            point.TimeMs, point.Accuracy, IsMiss: point.Accuracy == 0)).ToArray();
        var profileColor = profile.Label.StartsWith("Baka", StringComparison.Ordinal) ? PinkColor : CyanColor;
        QueuePlot(graph.transform, graph.GetComponent<RectTransform>(), "CVT:" + profile.Label,
            points, 0, maxTime, false, false, profileColor, profileColor, MissColor, BorderColor, profileColor);
        AddPercentLabels(graph.transform, height, true);

        var axis = Horizontal(parent, 0, 0, 0, 0, 0);
        axis.GetComponent<LayoutElement>().preferredHeight = 18;
        AddText(axis.transform, Text("开局", "Start"), 10, FontStyles.Normal, SubtleColor, 16, false, 1);
        var middle = AddText(axis.transform, Text("单键 ACC · 时间 →", "Per-note ACC · time →"), 10,
            FontStyles.Normal, SubtleColor, 16, false, 1);
        middle.alignment = TextAlignmentOptions.Center;
        var end = AddText(axis.transform, Text("结束", "End"), 10, FontStyles.Normal, SubtleColor, 16, false, 1);
        end.alignment = TextAlignmentOptions.Right;
    }

    private static void AddWarnings(Transform parent, IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0) return;
        var block = Block(parent, "Warnings", 14, 12, 14, 12, 6);
        foreach (var warning in warnings)
            AddText(block.transform, warning, 13, FontStyles.Normal, Hex("ffbd59"), 26, true);
    }

    private static GameObject Grid(Transform parent, int columns, float cellWidth, float cellHeight, float spacing)
    {
        var go = NewRect("Grid", parent);
        go.AddComponent<GridLayoutGroup>();
        go.AddComponent<LayoutElement>();
        var grid = go.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.spacing = new Vector2(spacing, spacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        var element = go.GetComponent<LayoutElement>();
        element.flexibleWidth = 1;
        return go;
    }

    private static GameObject Block(Transform parent, string name, int left, int right, int top, int bottom, float spacing)
    {
        var block = Panel(parent, name, SurfaceColor);
        Vertical(block.transform, left, right, top, bottom, spacing);
        return block;
    }

    private static GameObject Panel(Transform parent, string name, Color color)
    {
        var go = NewRect(name, parent);
        go.AddComponent<Image>();
        go.AddComponent<Outline>();
        go.AddComponent<LayoutElement>();
        go.GetComponent<Image>().color = color;
        var outline = go.GetComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1, -1);
        outline.useGraphicAlpha = true;
        go.GetComponent<LayoutElement>().flexibleWidth = 1;
        return go;
    }

    private static VerticalLayoutGroup Vertical(Transform parent, int left, int right, int top, int bottom, float spacing)
    {
        var layout = parent.gameObject.GetComponent<VerticalLayoutGroup>() ?? parent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = Padding(left, right, top, bottom);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fit = parent.gameObject.GetComponent<ContentSizeFitter>() ?? parent.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return layout;
    }

    private static HorizontalLayoutGroup Horizontal(Transform parent, int left, int right, int top, int bottom, float spacing)
    {
        var go = NewRect("Row", parent);
        go.AddComponent<HorizontalLayoutGroup>();
        go.AddComponent<LayoutElement>();
        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.padding = Padding(left, right, top, bottom);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        go.GetComponent<LayoutElement>().flexibleWidth = 1;
        return layout;
    }

    private static TMP_Text AddText(Transform parent, string value, float size, FontStyles style, Color color,
        float preferredHeight, bool wrap = false, float flexibleWidth = 0)
    {
        var go = NewRect("Text", parent);
        go.AddComponent<TextMeshProUGUI>();
        go.AddComponent<LayoutElement>();
        var text = go.GetComponent<TextMeshProUGUI>();
        ApplyTextStyle(text, value, size, style, color, wrap);
        var element = go.GetComponent<LayoutElement>();
        element.preferredHeight = preferredHeight;
        element.minHeight = preferredHeight;
        element.flexibleWidth = flexibleWidth;
        return text;
    }

    private static TMP_Text AddAnchoredText(Transform parent, string value, float size, FontStyles style, Color color,
        Vector2 position, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, bool wrap = false)
    {
        var go = NewRect("Text", parent);
        go.AddComponent<TextMeshProUGUI>();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        var text = go.GetComponent<TextMeshProUGUI>();
        ApplyTextStyle(text, value, size, style, color, wrap);
        return text;
    }

    private static Button Button(Transform parent, string label, string accessibleName, Color background, Color ink, float width, float height)
    {
        var go = NewRect(accessibleName, parent);
        go.AddComponent<Image>();
        go.AddComponent<Button>();
        var image = go.GetComponent<Image>();
        image.color = background;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        var labelGo = NewRect("Label", go.transform);
        labelGo.AddComponent<TextMeshProUGUI>();
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 2);
        labelRect.offsetMax = new Vector2(-10, -2);
        var text = labelGo.GetComponent<TextMeshProUGUI>();
        ApplyTextStyle(text, label, 14, FontStyles.Normal, ink, false);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return button;
    }

    private static Image Image(Transform parent, string name, Color color)
    {
        var go = NewRect(name, parent);
        go.AddComponent<Image>();
        go.GetComponent<Image>().color = color;
        return go.GetComponent<Image>();
    }

    private static GameObject NewRect(string name, Transform? parent = null)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        if (parent) rect.SetParent(parent, false);
        return go;
    }

    private static RectOffset Padding(int left, int right, int top, int bottom)
    {
        var padding = new RectOffset { left = left, right = right, top = top, bottom = bottom };
        return padding;
    }

    private static void ApplyTextStyle(TMP_Text text, string value, float size, FontStyles style, Color color, bool wrap)
    {
        text.font = textTemplate!.font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.text = value;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = wrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.richText = false;
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static string Signed(double value) => $"{(value > 0 ? "+" : string.Empty)}{value:0.00} ms";
    private static string OptionalSigned(double? value) => value is null ? "--" : Signed(value.Value);
    private static string Ms(double value) => $"{value:0.00} ms";
    private static string Text(string zh, string en) => chinese ? zh : en;
    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }

    private sealed class PendingPlot
    {
        internal PendingPlot(RectTransform container, RawImage image, ReplayPlotDefinition definition,
            Color positive, Color early, Color miss, Color grid, Color line)
        {
            Container = container;
            Image = image;
            Definition = definition;
            Positive = positive;
            Early = early;
            Miss = miss;
            Grid = grid;
            Line = line;
        }

        internal RectTransform Container { get; }
        internal RawImage Image { get; }
        internal ReplayPlotDefinition Definition { get; }
        internal Color Positive { get; }
        internal Color Early { get; }
        internal Color Miss { get; }
        internal Color Grid { get; }
        internal Color Line { get; }
        internal bool Rendered { get; set; }
    }
}
