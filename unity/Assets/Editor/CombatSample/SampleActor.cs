using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Editor
{
    // Editor-only audition. Candidate assets never replace Resources or enter the player build.
    internal sealed class SampleActor
    {
        internal readonly Transform Root;
        readonly GameObject model;
        readonly Dictionary<string,AnimationClip> clips;
        readonly Transform[] bones;
        readonly Vector3[] positions;
        readonly Quaternion[] rotations;
        string previous;
        float blend;
        readonly List<Material> materials=new List<Material>();
        internal SampleActor(string name)
        {
            string folder="Assets/Editor/CombatSample/Characters/"+name+"/";
            string path=folder+name+".fbx";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!prefab)throw new Exception("Sample model missing: "+path);
            Root=new GameObject(name+" sample").transform;
            var placement=new GameObject("Placement").transform;placement.SetParent(Root,false);
            model=UnityEngine.Object.Instantiate(prefab,placement,false);
            foreach(var a in model.GetComponentsInChildren<Animation>())a.enabled=false;
            clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c=>!c.name.StartsWith("__preview__"))
                .ToDictionary(c=>c.name.Substring(c.name.LastIndexOf('|')+1));
            foreach(var required in name=="Golza"?new[]{"Idle","Walk","Windup","Attack","Hurt","Defeat"}:
                new[]{"Idle","Walk","LeftPunch","RightPunch","Guard","Beam","Victory"})
                if(!clips.ContainsKey(required))throw new Exception(name+" missing "+required);
            clips["Idle"].SampleAnimation(model,0);
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
            Bounds bounds=default;bool first=true;
            foreach(var skin in skins)
            {
                skin.updateWhenOffscreen=true;skin.shadowCastingMode=ShadowCastingMode.On;skin.receiveShadows=true;
                var mesh=new Mesh();skin.BakeMesh(mesh,true);
                foreach(var v in mesh.vertices)
                {var p=skin.transform.TransformPoint(v);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                UnityEngine.Object.DestroyImmediate(mesh);
                var mats=skin.sharedMaterials;
                for(int i=0;i<mats.Length;i++){mats[i]=Surface(mats[i].name,name,folder);materials.Add(mats[i]);}
                skin.sharedMaterials=mats;
            }
            if(first)throw new Exception(name+" has no deforming mesh");
            float scale=(name=="Golza"?3.6f:3.45f)/bounds.size.y;
            // A long tail must not move the monster's pivot behind its feet.
            var pivot=Joint(name=="Golza"?"bip_pelvis":"hip").position;
            placement.localScale=Vector3.one*scale;
            placement.localPosition=-new Vector3(pivot.x,bounds.min.y,pivot.z)*scale;
            bones=model.GetComponentsInChildren<Transform>();positions=new Vector3[bones.Length];rotations=new Quaternion[bones.Length];
            foreach(var bone in bones)bone.gameObject.layer=30;
            Debug.Log($"[CombatSample] {name} clips={clips.Count} height={bounds.size.y*scale:F2} bones={bones.Length}");
        }
        static Material Surface(string key,string actor,string folder)
        {
            var mat=new Material(Resources.Load<Material>("PrototypeSurface"));mat.name=key;
            mat.color=new Color(.75f,.8f,.9f);mat.SetFloat("_Metallic",.35f);mat.SetFloat("_Glossiness",.5f);
            if(actor=="Golza")
            {
                bool eyes=key.Contains("Eyes");
                mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+(eyes?"GolzaEyes":"GolzaBody")+".png");
                mat.color=Color.white;mat.SetFloat("_Metallic",.03f);mat.SetFloat("_Glossiness",.22f);
                if(eyes){mat.EnableKeyword("_EMISSION");mat.SetTexture("_EmissionMap",mat.mainTexture);mat.SetColor("_EmissionColor",new Color(.55f,.35f,.15f));}
            }
            else if(key.Contains("Suit"))
            {mat.mainTexture=Resources.Load<Texture2D>("Characters/Tiga/TigaBody");mat.color=Color.white;mat.SetFloat("_Metallic",.12f);mat.SetFloat("_Glossiness",.4f);}
            else if(key.Contains("Gold"))mat.color=new Color(.8f,.55f,.18f);
            else if(key.Contains("EyeRim"))mat.color=new Color(.02f,.03f,.04f);
            else if(key.Contains("EyesGlow")||key.Contains("Timer")||key.Contains("Crystal"))
            {var c=key.Contains("EyesGlow")?new Color(1,.85f,.5f):new Color(.1f,.6f,1);mat.color=c;mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",c*1.5f);}
            return mat;
        }
        internal Transform Joint(string name)
        {foreach(var b in model.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception("Missing joint "+name);}
        internal void Opacity(float alpha)
        {
            foreach(var mat in materials)
            {
                var c=mat.color;c.a=alpha;mat.color=c;
                bool fade=alpha<.999f;
                mat.SetInt("_SrcBlend",(int)(fade?BlendMode.SrcAlpha:BlendMode.One));
                mat.SetInt("_DstBlend",(int)(fade?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));
                mat.SetInt("_ZWrite",fade?0:1);
                if(fade)mat.EnableKeyword("_ALPHABLEND_ON");else mat.DisableKeyword("_ALPHABLEND_ON");
                mat.renderQueue=fade?3000:-1;
            }
        }
        internal void Pose(string clip,float age,Vector3 position,Vector3 direction)
        {
            for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;}
            if(previous!=clip){blend=previous==null?0:.12f;previous=clip;}
            clips[clip].SampleAnimation(model,Mathf.Clamp(age,0,clips[clip].length));
            float mix=blend>0?Mathf.Clamp01((1/30f)/blend):1;blend=Mathf.Max(0,blend-1/30f);
            for(int i=1;i<bones.Length&&mix<1;i++)
            {bones[i].localPosition=Vector3.Lerp(positions[i],bones[i].localPosition,mix);bones[i].localRotation=Quaternion.Slerp(rotations[i],bones[i].localRotation,mix);}
            Root.position=position;Root.rotation=Quaternion.LookRotation(direction,Vector3.up);
        }
    }
}
