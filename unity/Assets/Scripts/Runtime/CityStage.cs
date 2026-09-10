using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Small streets and rooftops establish giant scale. City artwork stays in the distance.
    public sealed class CityStage : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        Cubemap reflections; Material previousSky; Texture previousReflection; DefaultReflectionMode previousMode;
        Material ground,concrete,roof,glass,light,road,car;
        readonly System.Random random=new System.Random(711);
        public static CityStage Create(Transform parent)
        {
            var stage=new GameObject("City battle district").AddComponent<CityStage>();
            stage.transform.SetParent(parent,false);stage.Build();return stage;
        }
        Material Surface(string name,Color color,float metallic=.1f,float smooth=.35f)
        {
            var m=new Material(Resources.Load<Material>("PrototypeSurface")){name=name,color=color};
            m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);materials.Add(m);return m;
        }
        void Build()
        {
            ground=new Material(Resources.Load<Shader>("CityGround"));materials.Add(ground);
            concrete=Surface("Architectural stone",new Color(.22f,.28f,.34f));
            roof=Surface("Roof plant and ledges",new Color(.06f,.105f,.15f),.4f,.5f);
            glass=new Material(Resources.Load<Shader>("DistrictBuilding"));materials.Add(glass);
            road=Surface("Asphalt",new Color(.035f,.05f,.075f),.12f,.5f);
            light=Surface("City illuminated signs",new Color(.22f,.63f,.8f));light.EnableKeyword("_EMISSION");light.SetColor("_EmissionColor",new Color(.12f,.55f,.85f)*1.8f);
            car=Surface("Parked vehicles",new Color(.57f,.57f,.53f),.55f,.55f);
            Box("City foundation",new Vector3(0,-.065f,6),new Vector3(70,.12f,60),ground);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Side avenue",new Vector3(side*4.1f,.002f,4),new Vector3(.8f,.012f,25),road);
                Box("Road lighting",new Vector3(side*3.68f,.013f,4),new Vector3(.018f,.013f,25),light);
                for(int z=-5;z<16;z++)
                {
                    Box("Avenue lane marker",new Vector3(side*4.1f,.011f,z+.3f),new Vector3(.014f,.008f,.28f),concrete);
                    if(z%3==0)Vehicle(new Vector3(side*4.35f,.05f,z));
                }
                for(int row=0;row<6;row++)
                {
                    float z=-.6f+row*2.2f,x=side*(5.1f+(row%2)*.4f),h=.85f+(float)random.NextDouble()*1.6f;
                    Building(x,z,1.0f+(float)random.NextDouble()*.5f,.95f,h,row);
                    Building(side*(7.2f+(row%2)*.4f),z+1,1.3f,1.15f,h*1.3f,row+2);
                }
            }
            Box("Cross avenue",new Vector3(0,.005f,5.4f),new Vector3(26,.012f,.85f),road);
            for(int i=-14;i<=14;i++)
            {
                Box("Cross avenue lane marker",new Vector3(i*.65f,.02f,5.4f),new Vector3(.28f,.012f,.015f),concrete);
                if(i%4==0)Vehicle(new Vector3(i*.8f,.06f,5.63f));
            }
            for(int i=-6;i<=6;i++)
            {
                float x=i*1.65f,h=Mathf.Abs(i)<2?1.05f:1.5f+(float)random.NextDouble()*2.8f;
                Building(x,8.4f+(i%2)*.55f,1.25f,1.3f,h,i+10);
                Building(x+.7f,12.4f,1.3f,1.6f,h*1.35f,i+20);
            }
            foreach(int side in new[]{-1,1})
            {
                Building(side*6.6f,-4.5f,2.2f,1.65f,.7f,3);
                Box("District light plinth",new Vector3(side*3.35f,.035f,3.9f),new Vector3(.24f,.06f,.38f),roof);
            }
            previousSky=RenderSettings.skybox;previousReflection=RenderSettings.customReflectionTexture;previousMode=RenderSettings.defaultReflectionMode;
            var sky=new Material(Resources.Load<Shader>("CitySky"));materials.Add(sky);RenderSettings.skybox=sky;
            reflections=new Cubemap(128,TextureFormat.RGBAHalf,true){name="City dusk reflection"};
            var capture=new GameObject("Environment capture").AddComponent<Camera>();capture.enabled=false;
            capture.clearFlags=CameraClearFlags.Skybox;capture.cullingMask=0;capture.allowHDR=true;
            capture.RenderToCubemap(reflections);Release(capture.gameObject);
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=reflections;RenderSettings.reflectionIntensity=.75f;
            CombineStaticGeometry();
        }
        void Building(float x,float z,float width,float depth,float height,int variation)
        {
            Box("Windowed tower",new Vector3(x,height/2,z),new Vector3(width,height,depth),glass);
            Box("Stone crown",new Vector3(x,height+.028f,z),new Vector3(width+.055f,.055f,depth+.055f),concrete);
            Box("Recessed roof",new Vector3(x,height+.07f,z),new Vector3(width*.83f,.035f,depth*.83f),roof);
            Box("Service penthouse",new Vector3(x-width*.2f,height+.18f,z+.1f),new Vector3(width*.28f,.22f,depth*.34f),roof);
            for(int i=0;i<2;i++)Box("Ventilation plant",new Vector3(x+width*.23f,height+.125f,z-depth*.22f+i*.22f),new Vector3(.2f,.11f,.15f),concrete);
            if(variation%3==0)
            {
                Box("Lit roof cornice",new Vector3(x,height-.03f,z-depth/2-.01f),new Vector3(width,.025f,.02f),light);
                Box("Radio mast",new Vector3(x,height+.48f,z),new Vector3(.022f,.82f,.022f),roof);
            }
            for(int edge=-1;edge<=1;edge+=2)
                Box("Tower vertical frame",new Vector3(x+edge*(width/2-.035f),height/2,z-depth/2-.015f),new Vector3(.055f,height,.025f),concrete);
            if(height>2.2f)Box("Tower setback",new Vector3(x,height+.22f,z),new Vector3(width*.65f,.38f,depth*.64f),glass);
        }
        void Vehicle(Vector3 p)
        {Box("Scale reference vehicle",p,new Vector3(.11f,.07f,.24f),car);Box("Vehicle roof",p+Vector3.up*.05f,new Vector3(.085f,.045f,.12f),roof);}
        void Box(string name,Vector3 p,Vector3 s,Material m)=>GameWorld.Primitive(name,PrimitiveType.Cube,transform,p,s,m);
        void CombineStaticGeometry()
        {
            // One renderer per material rather than hundreds of city draw calls.
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var filter in GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;
                if(!groups.TryGetValue(material,out var list))groups[material]=list=new List<CombineInstance>();
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                renderer.enabled=false;Release(filter.gameObject);
            }
            foreach(var pair in groups)
            {
                var mesh=new Mesh{name="City geometry "+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());
                var obj=new GameObject(mesh.name);obj.transform.SetParent(transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;RuntimeResources.Own(transform,mesh);
            }
        }
        void OnDestroy()
        {
            if(RenderSettings.customReflectionTexture==reflections)
            {RenderSettings.customReflectionTexture=previousReflection;RenderSettings.defaultReflectionMode=previousMode;RenderSettings.skybox=previousSky;}
            Release(reflections);foreach(var m in materials)Release(m);
        }
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
