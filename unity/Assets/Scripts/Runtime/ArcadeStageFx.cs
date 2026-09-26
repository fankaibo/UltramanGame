using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Short, in-engine transformation and victory beats. No prerecorded cutaway.
    public sealed class ArcadeStageFx
    {
        readonly LineRenderer[] helix=new LineRenderer[4];
        readonly LineRenderer[] victoryRings=new LineRenderer[2];
        readonly LineRenderer[] speedLines=new LineRenderer[10];
        readonly Transform veil;
        readonly Material veilMaterial;
        readonly Material victoryMaterial;
        readonly Material speedMaterial;
        readonly Light light;
        readonly Light victoryLight;
        readonly Vector3 hero;
        GamePhase previous;
        float age;
        public float PhaseAge=>age;
        public ArcadeStageFx(Transform root,Vector3 position)
        {
            hero=position;
            var mat=RuntimeResources.Own(root,new Material(Resources.Load<Shader>("SoftGlow")){color=Color.white});
            for(int i=0;i<helix.Length;i++)
            {
                var r=new GameObject("Transformation spiral").AddComponent<LineRenderer>();r.transform.SetParent(root,false);
                r.sharedMaterial=mat;r.positionCount=80;r.widthMultiplier=i==0?.055f:.022f;r.enabled=false;r.numCapVertices=3;helix[i]=r;
            }
            victoryMaterial=RuntimeResources.Own(root,new Material(Resources.Load<Shader>("SoftGlow")));
            for(int i=0;i<victoryRings.Length;i++)
            {
                var r=new GameObject("Victory energy ring").AddComponent<LineRenderer>();r.transform.SetParent(root,false);
                r.sharedMaterial=victoryMaterial;r.positionCount=72;r.widthMultiplier=i==0?.045f:.025f;r.enabled=false;r.numCapVertices=3;
                victoryRings[i]=r;
            }
            speedMaterial=RuntimeResources.Own(root,new Material(Resources.Load<Shader>("SoftGlow")));
            for(int i=0;i<speedLines.Length;i++)
            {
                var r=new GameObject("Arcade peripheral speed line").AddComponent<LineRenderer>();
                r.transform.SetParent(root,false);r.useWorldSpace=true;r.positionCount=2;
                r.sharedMaterial=speedMaterial;r.widthMultiplier=.009f+(i%3)*.003f;
                r.numCapVertices=3;r.enabled=false;speedLines[i]=r;
            }
            veilMaterial=RuntimeResources.Own(root,new Material(Resources.Load<Shader>("TransformationVeil")));
            veil=GameWorld.Primitive("Light transformation veil",PrimitiveType.Cylinder,root,hero+Vector3.up*2,new Vector3(1.8f,2.3f,1.8f),veilMaterial);
            veil.gameObject.SetActive(false);
            light=new GameObject("Transformation radiance").AddComponent<Light>();light.transform.SetParent(root,false);light.type=LightType.Point;light.range=7;light.color=new Color(.25f,.66f,1);light.intensity=0;
            victoryLight=new GameObject("Victory radiance").AddComponent<Light>();victoryLight.transform.SetParent(root,false);victoryLight.type=LightType.Point;victoryLight.range=8;victoryLight.color=new Color(1,.58f,.16f);victoryLight.intensity=0;
        }
        public void Clear() {foreach(var r in helix)r.enabled=false;foreach(var r in victoryRings)r.enabled=false;foreach(var r in speedLines)r.enabled=false;veil.gameObject.SetActive(false);light.intensity=0;victoryLight.intensity=0;}
        public void Tick(Battle battle,float dt,float clock)
        {
            if(previous!=battle.Phase) {age=0;previous=battle.Phase;}age+=dt;
            bool transform=battle.Phase==GamePhase.Transforming;
            float strength=transform?Mathf.Sin(Mathf.Clamp01(age/2.2f)*Mathf.PI):0;
            veil.gameObject.SetActive(transform&&strength>.02f);veilMaterial.SetFloat("_Strength",strength);veilMaterial.SetFloat("_Clock",age);
            for(int i=0;i<helix.Length;i++)
            {
                var r=helix[i];r.enabled=transform;
                if(!transform)continue;
                for(int j=0;j<80;j++)
                {
                    float t=j/79f,a=t*Mathf.PI*3+clock*(i%2==0?5:-5)+i*Mathf.PI*.5f;
                    float radius=.65f+Mathf.Sin(t*Mathf.PI)*.4f;
                    r.SetPosition(j,hero+new Vector3(Mathf.Cos(a)*radius,t*4.4f,Mathf.Sin(a)*radius));
                }
                r.startColor=new Color(.2f,.68f,1,strength*.6f);r.endColor=new Color(1,.87f,.53f,strength*.75f);
            }
            light.transform.position=hero+Vector3.up*2.5f;light.intensity=strength*3.5f;

            bool victory=battle.Phase==GamePhase.Victory;
            victoryLight.transform.position=hero+Vector3.up*1.9f;
            victoryLight.intensity=victory?Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.35f))*Mathf.Clamp01(1-age/4.2f)*2.8f:0;
            for(int i=0;i<victoryRings.Length;i++)
            {
                var ring=victoryRings[i];
                float delay=i*.22f;
                float t=Mathf.Clamp01((age-delay)/1.55f);
                ring.enabled=victory&&age>=delay&&age<3.8f;
                if(!ring.enabled)continue;
                float radius=.24f+Mathf.SmoothStep(0,1,t)*(1.55f+i*.52f);
                float y=.045f+.015f*i;
                for(int j=0;j<ring.positionCount;j++)
                {
                    float a=j*Mathf.PI*2/(ring.positionCount-1);
                    ring.SetPosition(j,hero+new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius));
                }
                float fade=(1-Mathf.Clamp01(t*.82f))*(.48f+.18f*Mathf.Sin(age*8+i));
                var color=new Color(1,.72f,.20f,Mathf.Max(.05f,fade));
                ring.startColor=color;ring.endColor=new Color(color.r,color.g,color.b,0);
            }

            // A short peripheral streak burst gives each committed strike the
            // visual rhythm of an arcade cabinet without covering either actor.
            // It is driven by the combat clock, so it cannot outrun gesture timing.
            var camera=Camera.main;
            float punchBurst=battle.Phase==GamePhase.Battle&&
                (battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch)
                ?Mathf.Sin(Mathf.Clamp01(battle.ActionAge/.42f)*Mathf.PI):0;
            float rushBurst=battle.Phase==GamePhase.Battle&&battle.Enemy==EnemyPhase.Attack
                ?Mathf.Sin(Mathf.Clamp01(battle.EnemyAge/Battle.EnemyAttackSeconds)*Mathf.PI):0;
            float hurtBurst=battle.Phase==GamePhase.Battle&&battle.Action==HeroAction.Hurt
                ?Mathf.Sin(Mathf.Clamp01(battle.ActionAge/.62f)*Mathf.PI):0;
            float burst=Mathf.Max(punchBurst,rushBurst*.92f,hurtBurst*.72f);
            if(camera&&burst>.015f&&battle.Action!=HeroAction.Beam)
            {
                Vector3 center=camera.transform.position+camera.transform.forward*(4.15f+.35f*burst);
                for(int i=0;i<speedLines.Length;i++)
                {
                    int band=i/2;float side=i%2==0?-1:1;
                    // The streaks live just inside the 25-degree battle lens;
                    // a wider offset would project outside a 16:9 TV frame.
                    float vertical=(band-2)*.34f+Mathf.Sin(clock*5.4f+i*1.7f)*.025f;
                    float lateral=side*(.86f+band*.025f);
                    Vector3 start=center+camera.transform.right*lateral+camera.transform.up*vertical;
                    Vector3 end=start-camera.transform.right*side*(.06f+burst*(.11f+(i%3)*.025f))
                        +camera.transform.up*side*(.11f+burst*.05f);
                    var line=speedLines[i];line.SetPosition(0,start);line.SetPosition(1,end);line.enabled=true;
                    Color color=rushBurst>=punchBurst?new Color(1,.38f,.12f,.10f+.18f*burst):
                        new Color(.20f,.72f,1,.09f+.16f*burst);
                    if(hurtBurst>punchBurst&&hurtBurst>rushBurst)color=new Color(1,.22f,.12f,.08f+.14f*burst);
                    line.startColor=color;line.endColor=new Color(color.r,color.g,color.b,0);
                    line.widthMultiplier=(.009f+(i%3)*.003f)*(1+burst*.45f);
                }
            }
            else foreach(var line in speedLines)line.enabled=false;
        }
    }
}
