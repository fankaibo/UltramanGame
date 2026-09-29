using System.Collections.Generic;
using UnityEngine;

namespace UltramanGame.Runtime
{
    // Fixed geometry and an explicit presentation clock make the live stage and
    // editor captures agree without touching the gesture or battle clocks.
    public sealed class VolcanoStage : MonoBehaviour
    {
        VolcanicEjecta ejecta;
        readonly Material[] cloudMaterials=new Material[2];
        const int AshCount=42;
        Mesh ashMesh;
        readonly Vector3[] ashVertices=new Vector3[AshCount*4];
        readonly Vector2[] ashStates=new Vector2[AshCount*4];
        readonly Vector3[] ashOrigins=new Vector3[42],ashVelocities=new Vector3[42];
        float ashMeshClock=-1;
        readonly Light[] ventLights=new Light[2];
        Light foregroundLavaLight;
        readonly Vector3[] vents={new Vector3(5.3f,0,13.5f),new Vector3(-5.7f,0,16.5f)};
        readonly System.Random random=new System.Random(903);
        Material ground,rock,lava,pool;
        public static VolcanoStage Create(Transform parent)
        {
            var stage=new GameObject("Basalt foothills").AddComponent<VolcanoStage>();
            stage.transform.SetParent(parent,false);stage.Build();return stage;
        }
        public static Texture3D CreatePlumeNoise()
        {
            // One small lattice shared by both vents. Hardware interpolation
            // replaces hundreds of per-pixel noise hashes along each ray.
            var data=new byte[32*32*32];new System.Random(929).NextBytes(data);
            var texture=new Texture3D(32,32,32,TextureFormat.R8,false)
            {name="Volcanic density lattice",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
            texture.SetPixelData(data,0);texture.Apply(false,true);return texture;
        }
        float Range(float min,float max)=>(float)random.NextDouble()*(max-min)+min;
        static float Height(float x,float z)
        {
            // The actors retain a flat floor; relief grows outside their footwork area.
            float edge=Mathf.SmoothStep(0,1,(new Vector2(x,z-.4f).magnitude-3.4f)/4);
            return edge*(Mathf.PerlinNoise(x*.34f+37,z*.28f+19)*.50f+Mathf.PerlinNoise(x*.9f+8,z*.8f+6)*.13f)-.012f;
        }
        void Build()
        {
            VolcanoEnvironment.Create(transform);
            ground=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("VolcanoGround")));
            Surface(ground,"rocks_ground_02","col",new Color(.63f,.67f,.72f),.30f,false);
            rock=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("VolcanoGround")));
            Surface(rock,"rock_face_03","diff",new Color(.46f,.50f,.56f),.37f,true);
            rock.SetFloat("_BackdropBlend",0);
            BuildTerrain();
            for(int i=0;i<64;i++)
            {
                float x=Range(-16,16),z=Range(-3,24);
                // Far terrain blends into the landscape plate. Keep freestanding
                // rocks on the actual near ground so they cannot float over its horizon.
                if(new Vector2(x,z-.4f).sqrMagnitude>100)continue;
                if(Mathf.Abs(x)<3.1f&&z<5)continue;
                float size=Range(.12f,.6f);
                Rock(new Vector3(x,Height(x,z)-.035f,z),new Vector3(size*Range(1.1f,2.1f),size*Range(.35f,.7f),size),rock);
            }
            // Low broken silhouettes frame the fight without hiding the characters.
            Rock(new Vector3(-5.8f,Height(-5.8f,5.8f),5.8f),new Vector3(1.35f,.86f,.95f),rock);
            Rock(new Vector3(6.4f,Height(6.4f,7.4f),7.4f),new Vector3(1.65f,.95f,1.1f),rock);
            BuildScree();
            VolcanicOutpost.Create(transform,Height);
            lava=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("VolcanicLava")));
            pool=RuntimeResources.Own(transform,new Material(lava));pool.SetFloat("_Pool",1);
            for(int i=0;i<vents.Length;i++)
            {
                Crater(vents[i],rock);
                LavaChannel(vents[i]+new Vector3(0,0,-.35f),6.8f,.42f,i,0);
                LavaChannel(vents[i]+new Vector3(.12f,0,-3.1f),2.7f,.26f,i+3,i==0?1.35f:-1.35f);
            }
            // Two staggered lava fountains share the deterministic stage clock
            // with their ash plumes, molten rocks and local light.
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
            if(Camera.main)Camera.main.depthTextureMode|=DepthTextureMode.Depth;
            var densityNoise=RuntimeResources.Own(transform,CreatePlumeNoise());
            for(int i=0;i<cloudMaterials.Length;i++)
            {
                var material=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("VolcanicPlume")));
                material.SetFloat("_Seed",i*3.71f);cloudMaterials[i]=material;
                material.SetTexture("_Noise",densityNoise);
                var origin=vents[i];origin.y=Height(origin.x,origin.z);
                var volume=GameWorld.Primitive("Volumetric volcanic ash",PrimitiveType.Cube,transform,
                    origin+new Vector3(.46f,2.83f,0),new Vector3(3.8f,5.6f,3.2f),material);
                var renderer=volume.GetComponent<Renderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            ejecta=new VolcanicEjecta(transform,vents,Height);
            // A sparse foreground ash layer adds depth between the camera and the
            // actors.  The deterministic loops keep editor reviews and live play
            // identical while the very low opacity leaves the gesture silhouette
            // readable for a child standing in front of the camera.
            var ashMaterial=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("AshMote")));
            ashMaterial.SetColor("_Color",new Color(.34f,.39f,.44f,.10f));
            var ashUv=new Vector2[AshCount*4];var ashTriangles=new int[AshCount*6];
            for(int i=0;i<AshCount;i++)
            {
                ashOrigins[i]=new Vector3(Range(-11,11),Range(.65f,5.1f),Range(1.5f,18.5f));
                ashVelocities[i]=new Vector3(Range(-.13f,.16f),Range(.025f,.10f),Range(.02f,.12f));
                int v=i*4,t=i*6;ashUv[v]=new Vector2(0,0);ashUv[v+1]=new Vector2(1,0);ashUv[v+2]=new Vector2(1,1);ashUv[v+3]=new Vector2(0,1);
                ashTriangles[t]=v;ashTriangles[t+1]=v+1;ashTriangles[t+2]=v+2;ashTriangles[t+3]=v;ashTriangles[t+4]=v+2;ashTriangles[t+5]=v+3;
            }
            ashMesh=RuntimeResources.Own(transform,new Mesh{name="Drifting volcanic ash",vertices=ashVertices,uv=ashUv,uv2=ashStates,triangles=ashTriangles});ashMesh.MarkDynamic();
            ashMesh.bounds=new Bounds(new Vector3(0,3,10),new Vector3(30,12,28));
            var ashObject=new GameObject("Drifting ash layer",typeof(MeshFilter),typeof(MeshRenderer));ashObject.transform.SetParent(transform,false);
            ashObject.GetComponent<MeshFilter>().sharedMesh=ashMesh;var ashRenderer=ashObject.GetComponent<MeshRenderer>();ashRenderer.sharedMaterial=ashMaterial;
            ashRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;ashRenderer.receiveShadows=false;
        }
        static void Surface(Material material,string asset,string colorMap,Color tint,float scale,bool face)
        {
            string path="Art/Basalt/"+asset+"_";
            material.SetTexture("_Albedo",Resources.Load<Texture2D>(path+colorMap+"_2k"));
            material.SetTexture("_Normal",Resources.Load<Texture2D>(path+"nor_gl_2k"));
            material.SetTexture("_ARM",Resources.Load<Texture2D>(path+"arm_2k"));
            material.SetColor("_Color",tint);material.SetFloat("_TextureScale",scale);
            material.SetFloat("_RockFace",face?1:0);material.SetFloat("_NormalStrength",face?.85f:.75f);
        }
        void BuildScree()
        {
            // Broken outcrops and their smaller fallen fragments belong together.
            // All centers are outside the actors' footwork and fall-recovery area.
            var debris=new System.Random(739);
            float Pick(float a,float b)=>(float)debris.NextDouble()*(b-a)+a;
            foreach(var center in new[]{new Vector3(-5.2f,0,5.3f),new Vector3(6.0f,0,6.5f),new Vector3(-4.1f,0,8.4f),new Vector3(3.9f,0,9.2f)})
            {
                for(int i=0;i<22;i++)
                {
                    float angle=Pick(0,Mathf.PI*2),distance=Pick(.45f,2.0f);
                    float x=center.x+Mathf.Cos(angle)*distance,z=center.z+Mathf.Sin(angle)*distance;
                    if(Mathf.Abs(x)<3.3f&&z<5.3f)continue;
                    float size=i<3?Pick(.5f,.9f):Pick(.055f,.26f);
                    Rock(new Vector3(x,Height(x,z)-.025f,z),new Vector3(size, size*Pick(.35f,.65f),size*Pick(.6f,1.25f)),rock);
                }
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
        void Crater(Vector3 center,Material rock)
        {
            const int sides=64,rings=5;var vertices=new Vector3[sides*rings];var uv=new Vector2[vertices.Length];var indices=new List<int>();
            float[] radii={.17f,.38f,.59f,.82f,1.12f},heights={.045f,.12f,.30f,.17f,-.008f};
            for(int ring=0;ring<rings;ring++)for(int side=0;side<sides;side++)
            {
                float angle=side*Mathf.PI*2/sides;
                float uneven=Mathf.Sin(angle*7+center.x)*.065f+Mathf.Sin(angle*13)*.035f;
                float r=radii[ring]*(1+uneven);
                float x=center.x+Mathf.Cos(angle)*r,z=center.z+Mathf.Sin(angle)*r;
                int v=ring*sides+side;vertices[v]=new Vector3(x,Height(x,z)+heights[ring]+uneven*heights[ring],z);uv[v]=new Vector2(x,z);
                if(ring==rings-1)continue;
                int next=ring*sides+(side+1)%sides;
                indices.AddRange(new[]{v,next,v+sides,next,next+sides,v+sides});
            }
            MeshObject("Cooled basalt vent rim",vertices,indices.ToArray(),uv,rock);
            vertices=new Vector3[sides+1];uv=new Vector2[sides+1];indices.Clear();
            vertices[0]=new Vector3(center.x,Height(center.x,center.z)+.04f,center.z);uv[0]=Vector2.one*.5f;
            for(int i=0;i<sides;i++)
            {float a=i*Mathf.PI*2/sides;var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));vertices[i+1]=vertices[0]+new Vector3(p.x*.42f,0,p.y*.42f);uv[i+1]=Vector2.one*.5f+p*.5f;indices.AddRange(new[]{0,(i+1)%sides+1,i+1});}
            MeshObject("Incandescent vent throat",vertices,indices.ToArray(),uv,pool);
        }
        void LavaChannel(Vector3 start,float length,float halfWidth,int seed,float branch)
        {
            const int rows=64,columns=8;var vertices=new Vector3[(rows+1)*(columns+1)];var uv=new Vector2[vertices.Length];var indices=new List<int>();
            for(int row=0;row<=rows;row++)
            {
                float progress=row/(float)rows,z=start.z-progress*length;
                float x=start.x+(Mathf.PerlinNoise(progress*3.6f,seed*3+4)-.5f)*.74f+branch*progress;
                float width=halfWidth*Mathf.Lerp(.8f,1.25f,Mathf.PerlinNoise(progress*13,seed+17))*(1-.7f*Mathf.Pow(progress,5));
                for(int column=0;column<=columns;column++)
                {
                    float cross=column/(float)columns*2-1,px=x+cross*width;
                    int at=row*(columns+1)+column;
                    float bank=Mathf.Pow(Mathf.Abs(cross),1.7f)*.055f;
                    vertices[at]=new Vector3(px,Height(px,z)+.025f+bank,z);uv[at]=new Vector2(column/(float)columns,progress*length);
                    if(row==rows||column==columns)continue;int next=at+columns+1;
                    indices.AddRange(new[]{at,at+1,next,at+1,next+1,next});
                }
            }
            MeshObject("Crusted flowing lava channel",vertices,indices.ToArray(),uv,lava);
            // Low rubble banks meet the hot channel at the same elevation. The
            // former bright ribbon sat on top of an unrelated flat ground plane.
            foreach(int side in new[]{-1,1})
            {
                const int bankColumns=5;var banks=new Vector3[(rows+1)*(bankColumns+1)];var bankUv=new Vector2[banks.Length];var bankIndices=new List<int>();
                for(int row=0;row<=rows;row++)
                {
                    float progress=row/(float)rows,z=start.z-progress*length;
                    float x=start.x+(Mathf.PerlinNoise(progress*3.6f,seed*3+4)-.5f)*.74f+branch*progress;
                    float width=halfWidth*Mathf.Lerp(.8f,1.25f,Mathf.PerlinNoise(progress*13,seed+17))*(1-.7f*Mathf.Pow(progress,5));
                    for(int column=0;column<=bankColumns;column++)
                    {
                        float across=column/(float)bankColumns;
                        float px=x+side*(width+across*(.28f+Mathf.PerlinNoise(z*3,seed+11)*.26f));
                        float ridge=Mathf.Sin(across*Mathf.PI)*(.065f+Mathf.PerlinNoise(z*4,seed+3)*.08f);
                        int at=row*(bankColumns+1)+column;
                        banks[at]=new Vector3(px,Height(px,z)+Mathf.Lerp(.080f,-.01f,across)+ridge,z);bankUv[at]=new Vector2(px,z);
                        if(row==rows||column==bankColumns)continue;int next=at+bankColumns+1;
                        if(side>0)bankIndices.AddRange(new[]{at,at+1,next,at+1,next+1,next});
                        else bankIndices.AddRange(new[]{at,next,at+1,at+1,next,next+1});
                    }
                }
                MeshObject("Cooled rubble channel bank",banks,bankIndices.ToArray(),bankUv,rock);
            }
        }
        void Rock(Vector3 p,Vector3 size,Material material)
        {
            // A fractured block with broad planar cuts, irregular shelves and
            // chipped edges. Subdivision changes the silhouette, not just shading.
            const int sides=24,rings=7;var vertices=new Vector3[sides*rings+2];vertices[0]=Vector3.down*.04f;
            float seed=p.x*1.73f+p.z*2.39f;
            vertices[vertices.Length-1]=Vector3.Scale(new Vector3(.04f,.94f,.03f),size);
            for(int ring=0;ring<rings;ring++)for(int i=0;i<sides;i++)
            {
                float v=ring/(float)(rings-1),a=i*Mathf.PI*2/sides;
                float profile=Mathf.Lerp(.88f,.64f,v)+Mathf.Sin(v*Mathf.PI)*.20f;
                float r=profile*(.88f+.13f*Mathf.Sin(a*3+seed)+.07f*Mathf.Sin(a*7-seed));
                r+=(Mathf.PerlinNoise(Mathf.Cos(a)*3+seed+31,v*5+Mathf.Sin(a)*2+17)-.5f)*.16f;
                var point=new Vector3(Mathf.Cos(a)*r,v*.91f+Mathf.Sin(a*4+seed)*.035f,Mathf.Sin(a)*r);
                // Two nonparallel fracture planes remove the rounded pebble look.
                point.x=Mathf.Min(point.x,.66f+point.y*.14f);
                point.z=Mathf.Max(point.z,-.69f+point.x*.16f);
                point.y=Mathf.Min(point.y,.86f+point.x*.13f-point.z*.09f);
                vertices[1+ring*sides+i]=Vector3.Scale(point,size);
            }
            var triangles=new List<int>();
            for(int i=0;i<sides;i++)
            {
                int next=(i+1)%sides;triangles.AddRange(new[]{0,1+i,1+next});
                for(int ring=0;ring<rings-1;ring++)
                {
                    int a=1+ring*sides+i,b=1+ring*sides+next,c=a+sides,d=b+sides;
                    triangles.AddRange(new[]{a,c,b,b,c,d});
                }
                triangles.AddRange(new[]{1+(rings-1)*sides+i,vertices.Length-1,1+(rings-1)*sides+next});
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
        public void SetBackdrop(Texture texture,Matrix4x4 worldToLocal,float clock)
        {
            ground.SetTexture("_BackdropTex",texture);ground.SetMatrix("_BackdropWorldToLocal",worldToLocal);ground.SetFloat("_Clock",clock);
        }
        public void Tick(float time)
        {
            lava.SetFloat("_Clock",time);pool.SetFloat("_Clock",time);
            for(int vent=0;vent<vents.Length;vent++)
            {
                var origin=vents[vent];origin.y=Height(origin.x,origin.z)+.05f;
                float cycle=Mathf.Repeat(time*.82f+vent*1.17f,2.8f)/2.8f;
                float envelope=Mathf.Sin(cycle*Mathf.PI);
                ventLights[vent].transform.position=origin+Vector3.up*.35f;
                ventLights[vent].intensity=envelope*2.4f;
                cloudMaterials[vent].SetFloat("_Clock",time);cloudMaterials[vent].SetFloat("_Surge",envelope);
            }
            float pulse=.5f+.5f*Mathf.Sin(time*2.15f+.7f);
            float eruption=Mathf.Sin(Mathf.Repeat(time*.82f,2.8f)/2.8f*Mathf.PI);
            foregroundLavaLight.intensity=.16f+.12f*pulse+.26f*eruption;
            var lens=Camera.main;
            ejecta.Tick(time,lens);
            // The tiny foreground motes do not need a vertex upload on every render
            // tick.  Updating at 45 Hz keeps their motion fluid while leaving the
            // render thread headroom for skeletal animation and camera compositing.
            if(ashMesh==null||ashMeshClock>=0&&time-ashMeshClock<1f/45f)return;
            ashMeshClock=time;
            Vector3 viewRight=lens?lens.transform.right:Vector3.right,viewUp=lens?lens.transform.up:Vector3.up;
            for(int i=0;i<AshCount;i++)
            {
                float life=9.5f,age=Mathf.Repeat(time*.42f+i*.61f,life),phase=age/life;
                var point=ashOrigins[i]+ashVelocities[i]*age+new Vector3(Mathf.Sin(time*.32f+i)*.08f,Mathf.Sin(time*.51f+i*1.7f)*.08f,0);
                float size=(.035f+(i%5)*.012f)*(.55f+.45f*Mathf.Sin(phase*Mathf.PI))*.5f,spin=Mathf.Sin(time*.7f+i)*.31f;
                Vector3 right=(viewRight*Mathf.Cos(spin)+viewUp*Mathf.Sin(spin))*size,up=(-viewRight*Mathf.Sin(spin)+viewUp*Mathf.Cos(spin))*size;
                int v=i*4;ashVertices[v]=point-right-up;ashVertices[v+1]=point+right-up;ashVertices[v+2]=point+right+up;ashVertices[v+3]=point-right+up;
                for(int j=0;j<4;j++)ashStates[v+j]=new Vector2(i*5.17f,phase);
            }
            ashMesh.vertices=ashVertices;ashMesh.uv2=ashStates;
        }
    }
}
