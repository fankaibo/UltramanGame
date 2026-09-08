using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Authored from primitives. Replace this presentation layer with a finished rigged Tiga model later.
    public sealed class PrototypeActor
    {
        public readonly Transform Root;
        readonly Transform[] arms=new Transform[4];
        readonly Transform[] fists=new Transform[2];
        readonly bool monster;
        readonly Vector3 leftShoulder=new Vector3(-.48f,2.1f,0), rightShoulder=new Vector3(.48f,2.1f,0);
        Vector3 le=new Vector3(-.68f,1.7f,0),re=new Vector3(.68f,1.7f,0),lw=new Vector3(-.65f,1.25f,.12f),rw=new Vector3(.65f,1.25f,.12f);
        public PrototypeActor(string name,Vector3 position,bool monster=false)
        {
            this.monster=monster;
            Root=new GameObject(name).transform; Root.position=position;
            var silver=Material(new Color(.76f,.85f,.92f),.6f);
            var red=Material(new Color(.66f,.06f,.19f));
            var purple=Material(new Color(.32f,.13f,.59f));
            var dark=Material(new Color(.14f,.22f,.29f));
            var gold=Material(new Color(1,.66f,.23f),.4f);
            var glow=Material(new Color(.1f,.8f,1),.2f,true);
            var body=monster?dark:purple;
            Part(PrimitiveType.Capsule,new Vector3(0,1.67f,0),new Vector3(.82f,.68f,.48f),body);
            Part(PrimitiveType.Sphere,new Vector3(0,2.62f,0),new Vector3(.62f,.77f,.54f),monster?dark:silver);
            if(!monster)
            {
                Part(PrimitiveType.Cube,new Vector3(0,2.95f,.02f),new Vector3(.08f,.47f,.40f),silver);
                Part(PrimitiveType.Sphere,new Vector3(-.17f,2.67f,.235f),new Vector3(.22f,.11f,.07f),glow);
                Part(PrimitiveType.Sphere,new Vector3(.17f,2.67f,.235f),new Vector3(.22f,.11f,.07f),glow);
                var a=Part(PrimitiveType.Cube,new Vector3(-.19f,2.0f,.27f),new Vector3(.46f,.10f,.055f),gold);
                a.localRotation=Quaternion.Euler(0,0,-25);
                a=Part(PrimitiveType.Cube,new Vector3(.19f,2.0f,.27f),new Vector3(.46f,.10f,.055f),gold);
                a.localRotation=Quaternion.Euler(0,0,25);
                Part(PrimitiveType.Sphere,new Vector3(0,1.94f,.29f),new Vector3(.16f,.21f,.075f),glow);
            }
            else
            {
                for(int i=0;i<3;i++) Part(PrimitiveType.Cube,new Vector3(0,2.0f+i*.3f,-.28f),new Vector3(.17f,.35f,.25f),gold);
                Part(PrimitiveType.Sphere,new Vector3(-.16f,2.68f,.24f),new Vector3(.16f,.1f,.1f),gold);
                Part(PrimitiveType.Sphere,new Vector3(.16f,2.68f,.24f),new Vector3(.16f,.1f,.1f),gold);
            }
            for(int side=0;side<2;side++)
            {
                float x=side==0?-.23f:.23f;
                Part(PrimitiveType.Capsule,new Vector3(x,.76f,0),new Vector3(.29f,.62f,.30f),monster?dark:red);
                Part(PrimitiveType.Sphere,new Vector3(x,.17f,.13f),new Vector3(.35f,.23f,.56f),monster?dark:silver);
                for(int section=0;section<2;section++) arms[side*2+section]=Part(PrimitiveType.Cylinder,Vector3.zero,Vector3.one,section==0?body:(monster?dark:silver));
                fists[side]=Part(PrimitiveType.Sphere,Vector3.zero,new Vector3(.25f,.25f,.25f),monster?gold:silver);
            }
            DrawArms();
        }
        public static Material Material(Color color,float metallic=0,bool emission=false)
        {
            var template=Resources.Load<Material>("PrototypeSurface");
            var m=template?new Material(template):new Material(Shader.Find("Standard")); m.color=color;
            m.SetFloat("_Metallic",metallic); m.SetFloat("_Glossiness",.45f);
            if(emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*1.8f); }
            return m;
        }
        Transform Part(PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(type); obj.transform.SetParent(Root,false);
            obj.transform.localPosition=position; obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            Object.Destroy(obj.GetComponent<Collider>());
            return obj.transform;
        }
        static void Bone(Transform bone,Vector3 a,Vector3 b)
        { bone.localPosition=(a+b)*.5f; bone.localRotation=Quaternion.FromToRotation(Vector3.up,b-a); bone.localScale=new Vector3(.18f,(b-a).magnitude*.5f,.18f); }
        void DrawArms()
        { Bone(arms[0],leftShoulder,le); Bone(arms[1],le,lw); Bone(arms[2],rightShoulder,re); Bone(arms[3],re,rw); fists[0].localPosition=lw; fists[1].localPosition=rw; }
        Vector3 Map(PosePoint p,PoseFrame frame)
        {
            var a=frame.points[11];var b=frame.points[12];
            float scale=Mathf.Max(.08f,PoseQuality.Distance(a,b));
            return new Vector3(Mathf.Clamp(-(p.x-(a.x+b.x)*.5f)/scale,-1.6f,1.6f),
                Mathf.Clamp(2.1f-(p.y-(a.y+b.y)*.5f)/scale, .85f,3.25f),.1f);
        }
        public void Update(PoseFrame pose,Battle game,float dt,float time)
        {
            Vector3 tle=new Vector3(-.70f,1.75f,0),tre=new Vector3(.70f,1.75f,0),tlw=new Vector3(-.55f,1.55f,.4f),trw=new Vector3(.55f,1.55f,.4f);
            if(!monster && pose!=null)
            {
                if(PoseQuality.Reliable(pose.points[13])) tle=Map(pose.points[13],pose);
                if(PoseQuality.Reliable(pose.points[14])) tre=Map(pose.points[14],pose);
                if(PoseQuality.Reliable(pose.points[15])) tlw=Map(pose.points[15],pose);
                if(PoseQuality.Reliable(pose.points[16])) trw=Map(pose.points[16],pose);
            }
            if(game.Phase==GamePhase.Transforming || game.Phase==GamePhase.Victory)
            { tlw=new Vector3(-.7f,3.25f,0);trw=new Vector3(.7f,3.25f,0);tle=new Vector3(-.8f,2.7f,0);tre=new Vector3(.8f,2.7f,0); }
            if(!monster && game.Shield)
            { tlw=new Vector3(-.1f,2.25f,.55f);trw=new Vector3(.1f,2.25f,.55f); }
            if(!monster && game.Action==HeroAction.Beam)
            { tle=new Vector3(-.1f,1.9f,.4f);tlw=new Vector3(-.1f,2.5f,.5f);tre=new Vector3(.65f,2.2f,.4f);trw=new Vector3(-.1f,2.2f,.5f); }
            if(!monster && (game.Action==HeroAction.LeftPunch || game.Action==HeroAction.RightPunch))
            {
                float reach=Mathf.Sin(Mathf.Clamp01(game.ActionAge/.45f)*Mathf.PI);
                if(game.Action==HeroAction.LeftPunch) { tle=new Vector3(-.3f,2.05f,.5f);tlw=new Vector3(-.1f,2.1f,.4f+reach); }
                else { tre=new Vector3(.3f,2.05f,.5f);trw=new Vector3(.1f,2.1f,.4f+reach); }
            }
            if(monster && game.Enemy==EnemyPhase.Windup) { trw=new Vector3(.65f,3.3f,.25f);tre=new Vector3(.75f,2.65f,0); }
            float blend=1-Mathf.Exp(-dt*18);
            le=Vector3.Lerp(le,tle,blend);re=Vector3.Lerp(re,tre,blend);lw=Vector3.Lerp(lw,tlw,blend);rw=Vector3.Lerp(rw,trw,blend);DrawArms();
            Root.localScale=Vector3.one*(monster&&game.Phase==GamePhase.Victory?Mathf.Max(.05f,Root.localScale.x-dt):1);
        }
    }
}
