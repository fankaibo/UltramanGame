using System;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // The photo uses the selected battle model and its real materials. Freeze
    // the authored victory pose once, so countdowns and retakes never resize it.
    public sealed class PhotoHero
    {
        public readonly Transform Root;
        public readonly PhotoBody Body;
        public readonly float Scale;
        public PhotoHero(string id,Transform parent,Camera camera)
        {
            var actor=new AnimatedActor(id,Vector3.zero,Vector3.back);
            Root=actor.Root;
            try
            {
                if(!actor.IsRigged)throw new InvalidOperationException("Photo requires the selected 3D hero: "+id);
                actor.PosePhoto();
                Transform head=null,left=null,right=null;
                foreach(var bone in Root.GetComponentsInChildren<Transform>())
                {
                    if(bone.name=="head"||bone.name=="bip_head")head=bone;
                    if(bone.name=="armBase_L"||bone.name=="bip_upperArm_L")left=bone;
                    if(bone.name=="armBase_R"||bone.name=="bip_upperArm_R")right=bone;
                    bone.gameObject.layer=31;
                }
                if(!head||!left||!right)throw new InvalidOperationException("Missing photo body anchors: "+id);
                Bounds bounds=default;bool first=true;float crown=float.NegativeInfinity;
                // Some imports attach the helmet and lenses as rigid meshes to
                // the head bone. Include them before adding our frozen skins.
                foreach(var renderer in Root.GetComponentsInChildren<MeshRenderer>())
                {
                    var filter=renderer.GetComponent<MeshFilter>();if(!renderer.enabled||!filter||!filter.sharedMesh)continue;
                    foreach(var vertex in filter.sharedMesh.vertices)
                    {
                        var p=filter.transform.TransformPoint(vertex);
                        if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                        if(filter.transform.IsChildOf(head))crown=Mathf.Max(crown,p.y);
                    }
                }
                foreach(var skin in Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(!skin.enabled)continue;
                    var mesh=RuntimeResources.Own(Root,new Mesh{name=id+" photo victory mesh"});skin.BakeMesh(mesh,true);
                    var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                    var vertices=mesh.vertices;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var p=skin.transform.TransformPoint(vertices[i]);
                        if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                        // A raised fist must not become the person's head reference.
                        if(i<weights.Length&&HeadWeight(weights[i],bones,head)>.5f)crown=Mathf.Max(crown,p.y);
                    }
                    var frozen=new GameObject("Photo victory surface",typeof(MeshFilter),typeof(MeshRenderer));
                    frozen.layer=31;frozen.transform.SetParent(skin.transform,false);
                    frozen.GetComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=frozen.GetComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;
                    renderer.shadowCastingMode=skin.shadowCastingMode;renderer.receiveShadows=skin.receiveShadows;
                    skin.enabled=false;
                }
                if(first)throw new InvalidOperationException("Missing skinned photo geometry: "+id);
                Body=new PhotoBody(bounds.min.x,bounds.max.x,bounds.min.y,bounds.max.y,
                    (left.position.x+right.position.x)/2,(left.position.y+right.position.y)/2,crown);
                if(!Body.Valid)throw new InvalidOperationException("Invalid photo body measurements: "+id);
                Scale=Mathf.Min(6.5f/bounds.size.x,7.45f/bounds.size.y);
                // The sampled root faces -Z; preserve that rotation while placing
                // its already measured world-space silhouette in the photo scene.
                Root.SetParent(parent,false);Root.localScale=Vector3.one*Scale;
                Root.localPosition=new Vector3(-3.9f-bounds.center.x*Scale,-3.9f-bounds.min.y*Scale,0);
                Debug.Log($"[PhotoHero] id={id} geometry=3D fixedScale={Scale:F3} crown={crown:F3} shoulder={Body.Shoulder:F3}");
            }
            catch
            {
                if(Application.isPlaying)UnityEngine.Object.Destroy(Root.gameObject);else UnityEngine.Object.DestroyImmediate(Root.gameObject);
                throw;
            }
        }
        static float HeadWeight(BoneWeight w,Transform[] bones,Transform head)
        {
            return Weight(w.boneIndex0,w.weight0,bones,head)+Weight(w.boneIndex1,w.weight1,bones,head)+
                Weight(w.boneIndex2,w.weight2,bones,head)+Weight(w.boneIndex3,w.weight3,bones,head);
        }
        static float Weight(int i,float w,Transform[] bones,Transform head)
        {return w>0&&i<bones.Length&&bones[i]&&(bones[i]==head||bones[i].IsChildOf(head))?w:0;}
    }
}
