using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BetterMalodyV.Plugin;

internal readonly record struct ReplayPlotPoint(double X, double Y, bool IsEarly = false, bool IsMiss = false);

internal sealed record ReplayPlotDefinition(
    string Id,
    IReadOnlyList<ReplayPlotPoint> Points,
    double MinimumX,
    double MaximumX,
    bool ConnectPoints,
    bool Signed);

internal static class ReplayPlotTexture
{
    internal const float PlotLeft = 42;
    internal const float PlotRight = 16;
    internal const float PlotTop = 14;
    internal const float PlotBottom = 28;
    internal const float NormalPointRadius = 2.2f;
    internal const float MissPointRadius = 2.7f;

    internal static Texture2D Create(int width, int height, IReadOnlyList<ReplayPlotPoint> points,
        double minimumX, double maximumX, bool connectPoints, Color positiveColor, Color earlyColor,
        Color missColor, Color gridColor, Color lineColor)
    {
        width = Math.Clamp(width, 320, 2000);
        height = Math.Clamp(height, 64, 320);
        var pixels = new Color32[checked(width * height)];
        var plotRight = width - PlotRight;
        var plotTop = height - PlotTop;
        var plotBottom = PlotBottom;
        var plotWidth = plotRight - PlotLeft;
        var plotHeight = plotTop - plotBottom;

        for (var step = 0; step <= 4; step++)
        {
            var y = plotTop - plotHeight * step / 4f;
            var row = Mathf.Clamp(Mathf.RoundToInt(y), 0, height - 1);
            for (var x = (int)PlotLeft; x < plotRight; x++) BlendPixel(pixels, width, height, x, row, gridColor, 1);
        }

        Vector2 Position(ReplayPlotPoint point)
        {
            var xRatio = Mathf.Clamp01((float)((point.X - minimumX) / Math.Max(1, maximumX - minimumX)));
            var yRatio = Mathf.Clamp01((float)point.Y);
            return new Vector2(PlotLeft + xRatio * plotWidth, plotBottom + yRatio * plotHeight);
        }

        if (connectPoints)
        {
            for (var index = 1; index < points.Count; index++)
                DrawLine(pixels, width, height, Position(points[index - 1]), Position(points[index]), 1, lineColor);
        }

        foreach (var point in points)
        {
            var color = point.IsMiss ? missColor : point.IsEarly ? earlyColor : positiveColor;
            DrawCircle(pixels, width, height, Position(point), point.IsMiss ? MissPointRadius : NormalPointRadius, color);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(new Il2CppStructArray<Color32>(pixels));
        texture.Apply(false, false);
        return texture;
    }

    private static void DrawCircle(Color32[] pixels, int width, int height, Vector2 center, float radius, Color color)
    {
        var extent = radius + 1;
        var left = (int)Math.Floor(center.x - extent);
        var right = (int)Math.Ceiling(center.x + extent);
        var bottom = (int)Math.Floor(center.y - extent);
        var top = (int)Math.Ceiling(center.y + extent);
        for (var y = bottom; y <= top; y++)
        for (var x = left; x <= right; x++)
        {
            var dx = x + .5f - center.x;
            var dy = y + .5f - center.y;
            var coverage = Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
            if (coverage > 0) BlendPixel(pixels, width, height, x, y, color, coverage);
        }
    }

    private static void DrawLine(Color32[] pixels, int width, int height, Vector2 start, Vector2 end, float thickness, Color color)
    {
        var delta = end - start;
        var lengthSquared = delta.sqrMagnitude;
        if (lengthSquared < .01f) return;
        var extent = thickness + 1;
        var left = (int)Math.Floor(Math.Min(start.x, end.x) - extent);
        var right = (int)Math.Ceiling(Math.Max(start.x, end.x) + extent);
        var bottom = (int)Math.Floor(Math.Min(start.y, end.y) - extent);
        var top = (int)Math.Ceiling(Math.Max(start.y, end.y) + extent);
        for (var y = bottom; y <= top; y++)
        for (var x = left; x <= right; x++)
        {
            var sample = new Vector2(x + .5f, y + .5f);
            var amount = Mathf.Clamp01(Vector2.Dot(sample - start, delta) / lengthSquared);
            var nearest = start + amount * delta;
            var coverage = Mathf.Clamp01(thickness / 2 + .5f - Vector2.Distance(sample, nearest));
            if (coverage > 0) BlendPixel(pixels, width, height, x, y, color, coverage);
        }
    }

    private static void BlendPixel(Color32[] pixels, int width, int height, int x, int y, Color source, float coverage)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
        var index = y * width + x;
        var destination = pixels[index];
        var sourceAlpha = Mathf.Clamp01(source.a * coverage);
        if (sourceAlpha <= 0) return;
        var destinationAlpha = destination.a / 255f;
        var outputAlpha = sourceAlpha + destinationAlpha * (1 - sourceAlpha);
        var red = (source.r * sourceAlpha + destination.r / 255f * destinationAlpha * (1 - sourceAlpha)) / outputAlpha;
        var green = (source.g * sourceAlpha + destination.g / 255f * destinationAlpha * (1 - sourceAlpha)) / outputAlpha;
        var blue = (source.b * sourceAlpha + destination.b / 255f * destinationAlpha * (1 - sourceAlpha)) / outputAlpha;
        pixels[index] = new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(red) * 255),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(green) * 255),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(blue) * 255),
            (byte)Mathf.RoundToInt(outputAlpha * 255));
    }
}
