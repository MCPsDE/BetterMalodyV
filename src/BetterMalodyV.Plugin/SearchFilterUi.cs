using BetterMalodyV.Shared;
using Il2CppInterop.Runtime;
using Malody.Scene.Panel;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BetterMalodyV.Plugin;

internal static class SearchFilterUi
{
    public static void Attach(PanelSearchSong panel)
    {
        var side = panel.transform.Find("side");
        var middle = side ? side.Find("middle") : null;
        if (side is null || middle is null || !side || !middle) throw new NotSupportedException("Advanced-search layout does not match the supported game.");
        if (middle.Find("BetterMalodyV.Status")) return;
        var parent = side.Cast<RectTransform>();
        var previousSize = parent.sizeDelta;
        var root = CreateRect("BetterMalodyV.Status", middle);
        try
        {
        root.anchorMin = root.anchorMax = new Vector2(.5f,1); root.pivot = new Vector2(.5f,1);
        root.anchoredPosition = new Vector2(0,-135);
        var available = Math.Max(480,middle.Cast<RectTransform>().rect.width - 40);
        var font = panel.input.textComponent.font;
        // Some game language packs use a Latin-only search font. Use readable
        // English in that case instead of emitting missing CJK glyph boxes.
        var chinese = font.HasCharacter('谱') && font.HasCharacter('全') && font.HasCharacter('含');
        var statusLabel = Label(root,chinese ? "谱面状态" : "Status",font,0,144);
        var elements = new List<(RectTransform Rect,float Width)> { (statusLabel.rectTransform,Math.Max(144,statusLabel.GetPreferredValues(statusLabel.text).x+16)) };
        var buttons = new List<(StatusMask Flag, Toggle Toggle, Image Image)>();
        var options = new[] { (StatusMask.Stable,"Stable",180f), (StatusMask.Beta,"Beta",170f), (StatusMask.Alpha,chinese ? "Alpha (含 Promotion)" : "Alpha (Promotion)",320f) };
        void Paint()
        {
            foreach (var (flag,toggle,image) in buttons)
            {
                var on = (Plugin.Selection & flag) != 0;
                toggle.SetIsOnWithoutNotify(on);
                image.color = on ? new Color(.10f,.32f,.51f,.98f) : new Color(.12f,.14f,.18f,.98f);
            }
        }
        foreach (var (flag,label,minimumWidth) in options)
        {
            var rect = CreateRect(label,root); rect.anchorMin = rect.anchorMax = new Vector2(0,.5f);
            rect.pivot = new Vector2(0,.5f); rect.sizeDelta = new Vector2(minimumWidth,48);
            var image = rect.gameObject.AddComponent<Image>();
            var toggle = rect.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = image;
            var box = CreateRect("Checkbox",rect); box.anchorMin = box.anchorMax = new Vector2(0,.5f);
            box.anchoredPosition = new Vector2(23,0); box.sizeDelta = new Vector2(20,20);
            var frame = box.gameObject.AddComponent<Image>(); frame.color = new Color(.7f,.75f,.82f,1);
            var tick = CreateRect("Checked",box); tick.sizeDelta = new Vector2(12,12);
            var mark = tick.gameObject.AddComponent<Image>(); mark.color = new Color(.1f,.55f,.95f,1);
            toggle.graphic = mark;
            var text = Label(rect,label,font,44,minimumWidth-52);
            var width = Math.Max(minimumWidth,text.GetPreferredValues(label).x + 56);
            rect.sizeDelta = new Vector2(width,48); text.rectTransform.sizeDelta = new Vector2(width-52,48);
            elements.Add((rect,width));
            buttons.Add((flag,toggle,image));
            toggle.onValueChanged.AddListener((UnityAction<bool>)(on => { Plugin.SetSelection(on ? Plugin.Selection | flag : Plugin.Selection & ~flag); Paint(); }));
        }
        var (all,allText,_) = Button(root,chinese ? "全部" : "All",font,0,144);
        elements.Add((all.GetComponent<RectTransform>(),Math.Max(144,allText.GetPreferredValues(allText.text).x+28)));
        all.onClick.AddListener((UnityAction)(() => { Plugin.SetSelection(StatusMask.All); Paint(); }));
        float x = 0, y = 0;
        foreach (var (rect,width) in elements)
        {
            if (x > 0 && x + width > available) { x = 0; y += 60; }
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,48); x += width+12;
        }
        root.sizeDelta = new Vector2(available,y+60);
        parent.sizeDelta = previousSize + new Vector2(0,y+76);
        Paint();
        }
        catch
        {
            parent.sizeDelta = previousSize;
            root.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); throw;
        }
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>(); rect.SetParent(parent,false);
        go.layer = parent.gameObject.layer; return rect;
    }
    private static TextMeshProUGUI Label(RectTransform parent,string text,TMP_FontAsset font,float x,float width)
    {
        var rect = CreateRect("Label",parent); rect.anchorMin = rect.anchorMax = new Vector2(0,.5f);
        rect.pivot = new Vector2(0,.5f); rect.anchoredPosition = new Vector2(x,0); rect.sizeDelta = new Vector2(width,48);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font;
        label.fontSize = 24; label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.color = Color.white; label.text = text; label.raycastTarget = false;
        return label;
    }
    private static (Button,TextMeshProUGUI,Image) Button(RectTransform parent,string text,TMP_FontAsset font,float x,float width)
    {
        var rect = CreateRect(text,parent); rect.anchorMin = rect.anchorMax = new Vector2(0,.5f);
        rect.pivot = new Vector2(0,.5f); rect.anchoredPosition = new Vector2(x,0); rect.sizeDelta = new Vector2(width,48);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.12f,.14f,.18f,.98f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Label(rect,text,font,12,width-24);
        return (button,label,image);
    }
}
