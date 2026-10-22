using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using BetterMalodyV.Windows;

namespace BetterMalodyV.Manager;

/// <summary>Native, selectable document. Markdown is parsed as text, never executed as HTML.</summary>
internal sealed class MarkdownView : RichTextBox
{
    private readonly List<Font> fonts = new();
    public MarkdownView()
    {
        Dock = DockStyle.Fill; BorderStyle = BorderStyle.None; ReadOnly = true;
        BackColor = AppVisuals.Canvas; ForeColor = AppVisuals.Ink;
        DetectUrls = true; HideSelection = false; ScrollBars = RichTextBoxScrollBars.Vertical;
        AccessibleName = "BetterMalodyV 优化与修改说明";
        LinkClicked += (_, e) =>
        {
            if (Uri.TryCreate(e.LinkText, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.ToString()) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, "无法打开链接"); }
        };
    }

    public void Render(string markdown)
    {
        var position = GetCharIndexFromPosition(new Point(1, 1));
        Clear();
        foreach (var font in fonts) font.Dispose();
        fonts.Clear();
        foreach (var block in Markdown.Parse(markdown)) RenderBlock(block, 0);
        Select(Math.Min(position, TextLength), 0); ScrollToCaret();
    }

    private void RenderBlock(Block block, int depth)
    {
        if (block is ListBlock list)
        {
            foreach (var item in list) RenderBlock(item, depth + 1);
            AppendText("\n"); return;
        }
        if (block is LeafBlock leaf)
        {
            var size = block is HeadingBlock heading ? heading.Level == 1 ? 23f : heading.Level == 2 ? 16f : 12f : 10.5f;
            var style = block is HeadingBlock ? FontStyle.Bold : FontStyle.Regular;
            SelectionIndent = depth * 22; SelectionHangingIndent = depth > 0 ? 12 : 0;
            SelectionBullet = depth > 0; SelectionColor = AppVisuals.Ink;
            WriteInline(leaf.Inline, size, style);
            if (leaf.Inline is null && leaf is CodeBlock code) Write(code.Lines.ToString(), size, style);
            AppendText("\n"); SelectionBullet = false; SelectionIndent = 0; SelectionHangingIndent = 0;
            if (depth == 0) AppendText("\n");
        }
        else if (block is ContainerBlock container)
            foreach (var child in container) RenderBlock(child, depth);
    }

    private void WriteInline(ContainerInline? container, float size, FontStyle style)
    {
        if (container is null) return;
        foreach (var inline in container)
            switch (inline)
            {
                case LiteralInline literal: Write(literal.Content.ToString(), size, style); break;
                case CodeInline code: Write(code.Content, size, style); break;
                case EmphasisInline emphasis: WriteInline(emphasis, size, style | (emphasis.DelimiterCount >= 2 ? FontStyle.Bold : FontStyle.Italic)); break;
                case LinkInline link:
                    WriteInline(link, size, style);
                    if (!string.IsNullOrWhiteSpace(link.Url)) Write(" (" + link.Url + ")", size, style);
                    break;
                case LineBreakInline: Write("\n", size, style); break;
                case ContainerInline nested: WriteInline(nested, size, style); break;
            }
    }

    private void Write(string value, float size, FontStyle style)
    {
        var font = fonts.FirstOrDefault(f => f.Size == size && f.Style == style);
        if (font is null) { font = AppVisuals.UiFont(size, style); fonts.Add(font); }
        SelectionFont = font; AppendText(value);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) foreach (var font in fonts) font.Dispose();
    }
}
