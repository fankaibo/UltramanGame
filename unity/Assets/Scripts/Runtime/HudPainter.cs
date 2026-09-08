using System.Collections.Generic;
using UnityEngine;

namespace UltramanGame.Runtime
{
    public sealed class HudPainter
    {
        public static readonly Color Ink=new Color(.91f,.95f,1),Muted=new Color(.56f,.68f,.81f),Cyan=new Color(.25f,.86f,1),Gold=new Color(1,.76f,.38f),Violet=new Color(.65f,.48f,1);
        readonly Font font;
        readonly Texture2D circle;
        readonly Dictionary<int,GUIStyle> styles=new Dictionary<int,GUIStyle>();
        public HudPainter(Font font)
        {
            this.font=font;circle=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++) pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))));
            circle.SetPixels(pixels);circle.Apply();
        }
        public void Dispose() { Object.Destroy(circle); }
        public void Box(Rect rect,Color color)
        { var before=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=before; }
        public void Dot(Vector2 center,float size,Color color)
        { var before=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(center.x-size/2,center.y-size/2,size,size),circle);GUI.color=before; }
        public void Rounded(Rect r,Color color,float radius=12)
        {
            radius=Mathf.Min(radius,r.height/2,r.width/2);
            Box(new Rect(r.x+radius,r.y,r.width-2*radius,r.height),color);
            Box(new Rect(r.x,r.y+radius,radius,r.height-2*radius),color);
            Box(new Rect(r.xMax-radius,r.y+radius,radius,r.height-2*radius),color);
            var before=GUI.color;GUI.color=color;
            GUI.DrawTextureWithTexCoords(new Rect(r.x,r.y,radius,radius),circle,new Rect(0,.5f,.5f,.5f));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax-radius,r.y,radius,radius),circle,new Rect(.5f,.5f,.5f,.5f));
            GUI.DrawTextureWithTexCoords(new Rect(r.x,r.yMax-radius,radius,radius),circle,new Rect(0,0,.5f,.5f));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax-radius,r.yMax-radius,radius,radius),circle,new Rect(.5f,0,.5f,.5f));
            GUI.color=before;
        }
        public void Panel(Rect r,Color accent,bool active=false)
        {
            Rounded(new Rect(r.x,r.y+4,r.width,r.height),new Color(0,0,0,.20f));
            Rounded(r,new Color(accent.r,accent.g,accent.b,active?.75f:.25f));
            Rounded(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(.028f,.055f,.105f,.94f),11);
        }
        GUIStyle Style(int size,TextAnchor align,bool bold)
        {
            int key=size*100+(int)align*2+(bold?1:0);
            if(!styles.TryGetValue(key,out var style))
            { style=new GUIStyle {font=font,fontSize=size,alignment=align,wordWrap=true,fontStyle=bold?FontStyle.Bold:FontStyle.Normal};styles[key]=style; }
            return style;
        }
        public void Text(Rect rect,string text,int size=18,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft,bool bold=false)
        { var style=Style(size,align,bold);style.normal.textColor=color??Ink;GUI.Label(rect,text,style); }
        public bool Button(Rect r,string text,Color? accent=null)
        {
            var c=accent??Cyan;bool hover=r.Contains(Event.current.mousePosition);
            Rounded(r,new Color(c.r,c.g,c.b,hover?.26f:.12f),8);
            Text(r,text,16,Ink,TextAnchor.MiddleCenter);
            var eventType=Event.current.type;
            bool clicked=GUI.Button(r,GUIContent.none,GUIStyle.none);
            if(Debug.isDebugBuild&&hover&&(eventType==EventType.MouseDown||eventType==EventType.MouseUp))
                Debug.Log($"[UI] button={text} event={eventType} clicked={clicked} enabled={GUI.enabled}");
            return clicked;
        }
        public void Bar(Rect r,float amount,Color color)
        { Rounded(r,new Color(.13f,.21f,.31f),r.height/2);if(amount>0)Rounded(new Rect(r.x,r.y,r.width*Mathf.Clamp01(amount),r.height),color,r.height/2); }
        public void Line(Vector2 a,Vector2 b,Color color,float width=3)
        {
            var matrix=GUI.matrix;float angle=Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg;
            // Compose in HUD coordinates before the outer screen scale (including Retina/non-uniform scale).
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,angle),Vector3.one);
            Box(new Rect(0,-width/2,Vector2.Distance(a,b),width),color);GUI.matrix=matrix;
            Dot(a,width,color);Dot(b,width,color);
        }
        public void Figure(Rect r,string pose,float time,Color color)
        {
            Vector2 Map(float x,float y)=>new Vector2(r.x+x*r.width,r.y+y*r.height);
            float wave=(Mathf.Sin(time*3)+1)/2;
            var ls=Map(.35f,.36f);var rs=Map(.65f,.36f);
            Vector2 le=Map(.22f,.57f),re=Map(.78f,.57f),lw=Map(.28f,.75f),rw=Map(.72f,.75f);
            if(pose=="transform") { le=Map(.17f,.26f);re=Map(.83f,.26f);lw=Map(.25f,.06f+wave*.05f);rw=Map(.75f,.06f+wave*.05f); }
            if(pose=="punch") { re=Map(.76f,.40f);rw=Map(.70f+wave*.28f,.33f);lw=Map(.4f,.49f); }
            if(pose=="shield") { lw=Map(.52f,.42f);rw=Map(.48f,.42f);le=Map(.18f,.53f);re=Map(.82f,.53f); }
            if(pose=="beam") { le=Map(.40f,.60f);lw=Map(.40f,.24f);re=Map(.80f,.48f);rw=Map(.41f,.48f); }
            Dot(Map(.5f,.17f),r.width*.19f,color);Line(Map(.5f,.32f),Map(.5f,.65f),color,4);
            Line(ls,rs,color,4);Line(ls,le,color);Line(le,lw,color);Line(rs,re,color);Line(re,rw,color);
            Line(Map(.5f,.65f),Map(.3f,.93f),color,4);Line(Map(.5f,.65f),Map(.7f,.93f),color,4);
            Dot(lw,5,Gold);Dot(rw,5,Gold);
        }
    }
}
