using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BetterMalodyV.Plugin;

internal static class NativeUi
{
    private static Sprite? checkmark;
    internal static Sprite Checkmark
    {
        get
        {
            if(checkmark) return checkmark!;
            const int size=64;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            for(var y=0;y<size;y++) for(var x=0;x<size;x++)
            {
                var distance=Math.Min(Segment(x,y,10,32,26,16),Segment(x,y,26,16,55,50));
                texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(4.2f-distance)));
            }
            texture.Apply(); texture.hideFlags=HideFlags.HideAndDontSave;
            checkmark=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),64);
            checkmark.hideFlags=HideFlags.HideAndDontSave; return checkmark;
        }
    }
    private static float Segment(float x,float y,float ax,float ay,float bx,float by)
    {
        var dx=bx-ax;var dy=by-ay;var t=Mathf.Clamp01(((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy));
        return Mathf.Sqrt(Mathf.Pow(x-ax-t*dx,2)+Mathf.Pow(y-ay-t*dy,2));
    }
    internal static Button Action(Transform parent,TMP_Text source,string label)
    {
        var go=new GameObject("BetterMalodyV.RoomUpdate");go.layer=parent.gameObject.layer;
        var rect=go.AddComponent<RectTransform>();rect.SetParent(parent,false);
        var image=go.AddComponent<Image>();image.color=new Color(.8f,.8f,.8f,.12f);
        var button=go.AddComponent<Button>();button.targetGraphic=image;
        var textGo=new GameObject("Label");textGo.layer=go.layer;
        var tr=textGo.AddComponent<RectTransform>();tr.SetParent(rect,false);
        tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(12,0);tr.offsetMax=new Vector2(-12,0);
        var text=textGo.AddComponent<TextMeshProUGUI>();text.font=source.font;text.fontSize=source.fontSize;text.color=source.color;
        text.alignment=TextAlignmentOptions.Center;text.enableWordWrapping=false;text.raycastTarget=false;text.text=label;
        rect.sizeDelta=new Vector2(text.GetPreferredValues(label).x+32,Math.Max(40,source.fontSize+16));
        return button;
    }
}
