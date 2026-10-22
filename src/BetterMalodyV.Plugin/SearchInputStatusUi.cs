using BetterMalodyV.Shared;
using Malody.Scene.Panel;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BetterMalodyV.Plugin;

internal static class SearchFilterUi
{
    internal const string RootName = "BetterMalodyV.Status";
    public static bool Attach(PanelSearchSong panel)
    {
        var input = panel.input;
        if (!input || !input.textComponent || !input.textViewport) return false;
        if (input.transform.Find(RootName)) return true;
        var root = CreateRect(RootName,input.transform);
        var viewport = input.textViewport;
        var previousRight = viewport.offsetMax;
        try
        {
            var source = input.textComponent;
            var fontSize = source.fontSize;
            var height = Math.Max(40,input.GetComponent<RectTransform>().rect.height-8);
            var items = new List<(RectTransform Rect,float Width)>();
            var toggles = new List<(StatusMask Flag,Toggle Toggle,Image Background)>();
            void Paint()
            {
                foreach (var (flag,toggle,background) in toggles)
                {
                    var on = (Plugin.Selection & flag) != 0;
                    toggle.SetIsOnWithoutNotify(on);
                    background.color = on ? new Color(.42f,.42f,.42f,.55f) : new Color(.32f,.32f,.32f,.30f);
                }
            }
            foreach (var (flag,label) in new[] { (StatusMask.Stable,"Stable"),(StatusMask.Beta,"Beta"),(StatusMask.Alpha,"Alpha") })
            {
                var rect = CreateRect("Status."+flag,root);
                var background = rect.gameObject.AddComponent<Image>();
                var toggle = rect.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = background;
                var box = CreateRect("Checkbox",rect);
                box.anchorMin = box.anchorMax = box.pivot = new Vector2(0,.5f);
                box.anchoredPosition = new Vector2(8,0); box.sizeDelta = new Vector2(20,20);
                box.gameObject.AddComponent<Image>().color = new Color(.75f,.75f,.75f,1);
                var tick = CreateRect("Checked",box);
                tick.anchorMin = tick.anchorMax = new Vector2(.5f,.5f); tick.sizeDelta = new Vector2(12,12);
                var mark = tick.gameObject.AddComponent<Image>(); mark.color = new Color(.92f,.92f,.92f,1);
                toggle.graphic = mark;
                var text = Label(rect,label,source,fontSize,36,height);
                var width = text.GetPreferredValues(label).x+48;
                rect.sizeDelta = new Vector2(width,height);
                text.rectTransform.sizeDelta = new Vector2(width-40,height);
                items.Add((rect,width)); toggles.Add((flag,toggle,background));
                toggle.onValueChanged.AddListener((UnityAction<bool>)(on =>
                {
                    Plugin.SetSelection(on ? Plugin.Selection|flag : Plugin.Selection&~flag); Paint();
                }));
            }
            var resetRect = CreateRect("All",root);
            var resetBackground = resetRect.gameObject.AddComponent<Image>(); resetBackground.color = new Color(.32f,.32f,.32f,.30f);
            var reset = resetRect.gameObject.AddComponent<Button>(); reset.targetGraphic = resetBackground;
            var resetText = Label(resetRect,"All",source,fontSize,10,height);
            var resetWidth = resetText.GetPreferredValues("All").x+24;
            resetRect.sizeDelta = new Vector2(resetWidth,height);
            resetText.rectTransform.sizeDelta = new Vector2(resetWidth-20,height);
            reset.onClick.AddListener((UnityAction)(() => { Plugin.SetSelection(StatusMask.All); Paint(); }));
            items.Add((resetRect,resetWidth));
            float x = 0;
            foreach (var (rect,width) in items)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,.5f);
                rect.anchoredPosition = new Vector2(x,0); x += width+8;
            }
            var contentWidth = x-8;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(1,.5f);
            root.anchoredPosition = new Vector2(-8,0); root.sizeDelta = new Vector2(contentWidth,height);
            // Keep text, placeholder, selection and caret to the left of the controls.
            viewport.offsetMax = new Vector2(previousRight.x-contentWidth-20,previousRight.y);
            Paint();
            Plugin.Current.LogUiAttached(panel,contentWidth,viewport.rect.width);
            return true;
        }
        catch
        {
            viewport.offsetMax = previousRight;
            root.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); throw;
        }
    }

    private static RectTransform CreateRect(string name,Transform parent)
    {
        var go = new GameObject(name); var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent,false); go.layer = parent.gameObject.layer; return rect;
    }
    private static TextMeshProUGUI Label(RectTransform parent,string value,TMP_Text source,float size,float x,float height)
    {
        var rect = CreateRect("Label",parent); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,.5f);
        rect.anchoredPosition = new Vector2(x,0); rect.sizeDelta = new Vector2(200,height);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = source.font; text.fontSize = size; text.color = source.color;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false; text.text = value;
        return text;
    }
}
