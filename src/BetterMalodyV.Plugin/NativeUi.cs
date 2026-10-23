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
    internal static Button Action(Transform parent,TMP_Text source,string label,string name="BetterMalodyV.RoomUpdate")
    {
        var go=new GameObject(name);go.layer=parent.gameObject.layer;
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
    internal static TMP_Text? PrimaryText(Component root)=>root.GetComponentsInChildren<TextMeshProUGUI>(true)
        .Where(t=>t.font && !string.IsNullOrEmpty(t.text))
        .OrderByDescending(t=>t.color.a*(t.color.r*.2126f+t.color.g*.7152f+t.color.b*.0722f)).ThenByDescending(t=>t.fontSize).FirstOrDefault();
    internal static void AlignActionRow(RectTransform parent,IReadOnlyList<RectTransform> existing,Button added)
        => AlignActionRow(parent,existing.Append(added.GetComponent<RectTransform>()).ToArray());
    internal static void AlignActionRow(RectTransform parent,IReadOnlyList<RectTransform> items)
    {
        var all=items.ToArray();
        var baseline=all.Average(r=>parent.InverseTransformPoint(r.TransformPoint(r.rect.center)).y);
        var height=all.Max(r=>r.rect.height);
        foreach(var rect in all)rect.sizeDelta=new Vector2(rect.sizeDelta.x,height);
        const float gap=24;
        var total=all.Sum(r=>r.rect.width)+gap*(all.Length-1);var x=-total/2;
        foreach(var item in all)
        {
            var desired=parent.TransformPoint(new Vector3(x+item.rect.width/2,baseline,0));
            item.position+=desired-item.TransformPoint(item.rect.center);x+=item.rect.width+gap;
        }
    }
    internal static void Message(string message,bool error=true)
    {
        var type=typeof(Malody.Scene.SceneRoom).Assembly.GetTypes().Single(t=>t.Namespace=="Malody.Manager" && t.GetMethods().Any(m=>m.Name=="ShowMessage" && m.GetParameters().Length==4));
        var method=type.GetMethods().Single(m=>m.Name=="ShowMessage" && m.GetParameters().Length==4 && m.GetParameters()[0].ParameterType==typeof(string));
        var parameters=method.GetParameters();
        method.Invoke(RoomGameApi.Singleton(type),new[]{(object)message,Enum.Parse(parameters[1].ParameterType,error?"Error":"TipNoRecord"),Enum.Parse(parameters[2].ParameterType,"Short"),(object)0});
    }
}
