using System.Collections.Generic;
using UnityEngine;

namespace UltramanGame.Runtime
{
    // Fixed geometry and an explicit presentation clock make the live stage and
    // editor captures agree without touching the gesture or battle clocks.
    public sealed class VolcanoStage : MonoBehaviour
    {
        readonly List<Transform> embers=new List<Transform>();
        readonly List<Vector3> emberVelocity=new List<Vector3>();
        readonly List<float> emberAge=new List<float>();
        readonly LineRenderer[] meteors=new LineRenderer[3];
        readonly LineRenderer[] lavaStreams=new LineRenderer[3];
        readonly Transform[] eruptionClouds=new Transform[32],lavaBombs=new Transform[64];
        readonly Material[] cloudMaterials=new Material[32];
        readonly Light[] ventLights=new Light[2];
        Light foregroundLavaLight;
        readonly Vector3[] vents={new Vector3(5.3f,0,13.5f),new Vector3(-5.7f,0,16.5f)};
        readonly System.Random random=new System.Random(903);
        Material ground,glow;
        public static VolcanoStage Create(Transform parent)
        {
            var stage=new GameObject("Basalt foothills").AddComponent<VolcanoStage>();
            stage.transform.SetParent(parent,false);stage.Build();return stage;
        }
        float Range(float min,float max)=>(float)random.NextDouble()*(max-min)+min;
        static float Height(float x,float z)
        {
            // The actors retain a flat floor; relief grows outside their footwork area.
            float edge=Mathf.SmoothStep(0,1,(new Vector2(x,z-.4f).magnitude-3.4f)/4);
            return edge*(Mathf.PerlinNoise(x*.34f+37,z*.28f+19)*.32f+Mathf.PerlinNoise(x*.9f+8,z*.8f+6)*.09f)-.012f;
        }
        void Build()
        {
            ground=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("VolcanoGround")));
            ground.SetColor("_Color",new Color(.17f,.18f,.19f));
            var rock=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("VolcanoGround")));
            rock.SetColor("_Color",new Color(.15f,.16f,.17f));
            rock.SetFloat("_BackdropBlend",0);
            glow=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("SoftGlow")){color=Color.white});
            var emberMaterial=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("SoftGlow")){color=new Color(1,.32f,.065f,.8f)});
            BuildTerrain();
            for(int i=0;i<64;i++)
            {
                float x=Range(-16,16),z=Range(-3,24);
                if(Mathf.Abs(x)<3.1f&&z<5)continue;
                float size=Range(.12f,.6f);
                Rock(new Vector3(x,Height(x,z)-.035f,z),new Vector3(size*Range(1.1f,2.1f),size*Range(.35f,.7f),size),rock);
            }
            // Low broken silhouettes frame the fight without hiding the characters.
            Rock(new Vector3(-5.8f,Height(-5.8f,5.8f),5.8f),new Vector3(1.35f,.86f,.95f),rock);
            Rock(new Vector3(6.4f,Height(6.4f,7.4f),7.4f),new Vector3(1.65f,.95f,1.1f),rock);
            Rock(new Vector3(-8.4f,Height(-8.4f,13),13),new Vector3(2.1f,1.15f,1.25f),rock);
            for(int i=0;i<lavaStreams.Length;i++)
            {
                var line=lavaStreams[i]=Line("Cooling lava seam",42,.018f);line.enabled=true;
                float sx=i==0?-5.3f:i==1?5.3f:8,sz=i<2?16:23;
                for(int j=0;j<42;j++)
                {
                    float z=sz-j*.17f,x=sx+(Mathf.PerlinNoise(j*.21f,3+i*2)-.5f)*.72f;
                    line.SetPosition(j,new Vector3(x,Height(x,z)+.014f,z));
                }
            }
            for(int i=0;i<meteors.Length;i++)meteors[i]=Line("Distant meteor",2,.013f);
            // Two staggered lava fountains add a readable moving layer behind the
            // fighters.  They are deliberately lightweight line effects so the
            // same deterministic stage clock works in live play and captures.
            for(int i=0;i<ventLights.Length;i++)
            {
                var lightObject=new GameObject("Volcano vent light");lightObject.transform.SetParent(transform,false);
                ventLights[i]=lightObject.AddComponent<Light>();ventLights[i].type=LightType.Point;
                ventLights[i].color=new Color(1,.20f,.045f);ventLights[i].range=9;ventLights[i].intensity=0;
            }
            // A low foreground source ties the cool moonlit actors to the warm
            // lava field. Its restrained pulse follows the same deterministic
            // eruption clock as the distant vents, so it never flickers randomly.
            var glowObject=new GameObject("Foreground lava bounce");glowObject.transform.SetParent(transform,false);
            foregroundLavaLight=glowObject.AddComponent<Light>();foregroundLavaLight.type=LightType.Point;
            foregroundLavaLight.color=new Color(1,.18f,.045f);foregroundLavaLight.range=8.5f;foregroundLavaLight.shadows=LightShadows.None;
            foregroundLavaLight.transform.position=new Vector3(-1.9f,1.0f,4.4f);
            for(int i=0;i<eruptionClouds.Length;i++)
            {
                var material=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("VolcanicPlume")));
                material.SetFloat("_Seed",i*3.71f);cloudMaterials[i]=material;
                eruptionClouds[i]=GameWorld.Primitive("Billowing volcanic ash",PrimitiveType.Quad,transform,Vector3.zero,Vector3.one,material);
            }
            var molten=RuntimeResources.Own(transform,new Material(Resources.Load<Material>("PrototypeSurface")));
            molten.color=new Color(.42f,.08f,.012f);molten.EnableKeyword("_EMISSION");molten.SetColor("_EmissionColor",new Color(3.4f,.72f,.05f));
            for(int i=0;i<lavaBombs.Length;i++)
                lavaBombs[i]=GameWorld.Primitive("Ballistic molten rock",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.055f,molten);
            for(int i=0;i<26;i++)
            {
                var t=GameWorld.Primitive("Vent ember",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*Range(.017f,.032f),emberMaterial);
                embers.Add(t);emberVelocity.Add(new Vector3(Range(-.38f,.38f),Range(1.3f,2.3f),Range(-.2f,.2f)));
                emberAge.Add(Range(0,3.8f));t.gameObject.SetActive(false);
            }
        }
        void BuildTerrain()
        {
            const int columns=100,rows=90;var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];
            var indices=new int[columns*rows*6];int at=0;
            for(int z=0;z<=rows;z++)for(int x=0;x<=columns;x++)
            {
                float px=Mathf.Lerp(-35,35,x/(float)columns),pz=Mathf.Lerp(-23,40,z/(float)rows);
                int i=z*(columns+1)+x;vertices[i]=new Vector3(px,Height(px,pz),pz);uv[i]=new Vector2(px*.1f,pz*.1f);
                if(x==columns||z==rows)continue;
                indices[at++]=i;indices[at++]=i+columns+1;indices[at++]=i+1;
                indices[at++]=i+1;indices[at++]=i+columns+1;indices[at++]=i+columns+2;
            }
            MeshObject("Uneven ash field",vertices,indices,uv,ground);
        }
        void Rock(Vector3 p,Vector3 size,Material material)
        {
            const int sides=9;var vertices=new Vector3[sides*3+2];vertices[0]=Vector3.zero;vertices[vertices.Length-1]=new Vector3(.04f,.95f,.03f);
            for(int ring=0;ring<3;ring++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,r=Range(.77f,1.15f)*(ring==0?.65f:ring==1?1:.65f);
                vertices[1+ring*sides+i]=Vector3.Scale(new Vector3(Mathf.Cos(a)*r,(ring==0?.035f:ring==1?.4f:.79f)+Range(-.10f,.10f),Mathf.Sin(a)*r),size);
            }
            vertices[vertices.Length-1]=Vector3.Scale(vertices[vertices.Length-1],size);
            var triangles=new List<int>();
            for(int i=0;i<sides;i++)
            {
                int next=(i+1)%sides;triangles.AddRange(new[]{0,1+i,1+next});
                for(int ring=0;ring<2;ring++)
                {
                    int a=1+ring*sides+i,b=1+ring*sides+next,c=a+sides,d=b+sides;
                    triangles.AddRange(new[]{a,c,b,b,c,d});
                }
                triangles.AddRange(new[]{1+2*sides+i,vertices.Length-1,1+2*sides+next});
            }
            var uv=new Vector2[vertices.Length];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vertices[i].x,vertices[i].z);
            var t=MeshObject("Weathered basalt",vertices,triangles.ToArray(),uv,material);
            t.localPosition=p;t.localRotation=Quaternion.Euler(0,Range(0,360),0);
        }
        Transform MeshObject(string name,Vector3[] vertices,int[] triangles,Vector2[] uv,Material material)
        {
            var mesh=RuntimeResources.Own(transform,new Mesh{name=name,vertices=vertices,triangles=triangles,uv=uv});
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(transform,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;return obj.transform;
        }
        LineRenderer Line(string name,int points,float width)
        {var r=new GameObject(name).AddComponent<LineRenderer>();r.transform.SetParent(transform,false);r.sharedMaterial=glow;r.positionCount=points;r.widthMultiplier=width;r.numCapVertices=3;r.enabled=false;return r;}
        public void SetBackdrop(Texture texture,Matrix4x4 worldToLocal,float clock)
        {
            ground.SetTexture("_BackdropTex",texture);ground.SetMatrix("_BackdropWorldToLocal",worldToLocal);ground.SetFloat("_Clock",clock);
        }
        static void ColorLine(LineRenderer line,Color color,float alpha)
        {color.a=alpha;line.startColor=color;line.endColor=color;}
        public void Tick(float time)
        {
            for(int i=0;i<meteors.Length;i++)
            {
                var r=meteors[i];float age=Mathf.Repeat(time+i*6.1f,19+i*2);float t=age/1.15f;r.enabled=t<1;
                if(!r.enabled)continue;
                var head=new Vector3(-9+i*7+t*3,9+i*.55f-t*2.8f,17+i*2);
                r.SetPosition(0,head);r.SetPosition(1,head+new Vector3(-.5f,.48f,0));
                r.startColor=new Color(.7f,.8f,1,Mathf.Sin(t*Mathf.PI)*.5f);r.endColor=new Color(.6f,.7f,1,0);
            }
            for(int i=0;i<lavaStreams.Length;i++)
            {var color=new Color(1,.25f,.055f,.18f+.07f*Mathf.Sin(time*.7f+i));lavaStreams[i].startColor=lavaStreams[i].endColor=color;}
            for(int vent=0;vent<vents.Length;vent++)
            {
                var origin=vents[vent];origin.y=Height(origin.x,origin.z)+.05f;
                float cycle=Mathf.Repeat(time*.82f+vent*1.17f,2.8f)/2.8f;
                float envelope=Mathf.Sin(cycle*Mathf.PI);
                ventLights[vent].transform.position=origin+Vector3.up*.35f;
                ventLights[vent].intensity=envelope*2.4f;
            }
            float pulse=.5f+.5f*Mathf.Sin(time*2.15f+.7f);
            float eruption=Mathf.Sin(Mathf.Repeat(time*.82f,2.8f)/2.8f*Mathf.PI);
            foregroundLavaLight.intensity=.16f+.12f*pulse+.26f*eruption;
            var lens=Camera.main;
            for(int i=0;i<eruptionClouds.Length;i++)
            {
                int vent=i%2;float life=5.6f,age=Mathf.Repeat(time+i*.397f,life),p=age/life;
                Vector3 origin=vents[vent];origin.y=Height(origin.x,origin.z);
                var cloud=eruptionClouds[i];float side=Mathf.Sin(i*9.17f);
                cloud.position=origin+new Vector3(side*(.10f+p*.85f)+p*.72f,.15f+age*.75f,Mathf.Cos(i*5.3f)*.3f);
                if(lens)cloud.rotation=lens.transform.rotation*Quaternion.Euler(0,0,side*25+age*9);
                float size=.48f+p*2.1f;cloud.localScale=new Vector3(size,size*1.15f,1);
                cloudMaterials[i].SetFloat("_Age",p);cloudMaterials[i].SetFloat("_Clock",age);
            }
            for(int i=0;i<lavaBombs.Length;i++)
            {
                float age=Mathf.Repeat(time+i*.073f,2.5f),phase=i*2.399f;
                Vector3 origin=vents[i%2];origin.y=Height(origin.x,origin.z)+.09f;
                Vector3 velocity=new Vector3(Mathf.Sin(phase)*(.28f+(i%4)*.13f),3.3f+(i%7)*.22f,Mathf.Cos(phase)*.5f);
                Vector3 point=origin+velocity*age+Vector3.down*2.7f*age*age;
                bool active=point.y>Height(point.x,point.z);lavaBombs[i].gameObject.SetActive(active);
                if(active){lavaBombs[i].position=point;lavaBombs[i].localScale=Vector3.one*(.027f+(i%5)*.007f)*(1-age*.13f);}
            }
            for(int i=0;i<embers.Count;i++)
            {
                float age=Mathf.Repeat(time+emberAge[i],3.8f);var origin=vents[i%vents.Length];origin.y=Height(origin.x,origin.z)+.04f;
                Vector3 p=origin+emberVelocity[i]*age+Vector3.down*.95f*age*age;
                bool active=age<2.1f&&p.y>Height(p.x,p.z);embers[i].gameObject.SetActive(active);if(active)embers[i].position=p;
            }
        }
    }
}
