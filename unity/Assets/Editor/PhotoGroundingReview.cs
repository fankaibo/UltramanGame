using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class PhotoGroundingReview
    {
        public static void Composition(){PhotoHeroReview.Run();PhotoFramingReview.After();}
        public static void Run()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/photo-grounding-20261010/inspection"));
            Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var actor=new AnimatedActor(id,Vector3.zero,Vector3.back);actor.Update(new Battle(),null,0,0,7);
                var bones=actor.Root.GetComponentsInChildren<Transform>();
                var feet=new[]{Bone(bones,"Foot_L","bip_foot_L"),Bone(bones,"Foot_R","bip_foot_R")};
                var thighs=new[]{Bone(bones,"ThighBase_L","bip_hip_L"),Bone(bones,"ThighBase_R","bip_hip_R")};
                var knees=new[]{Bone(bones,"Shin_L","bip_knee_L"),Bone(bones,"Shin_R","bip_knee_R")};
                var lengths=new float[4];var before=new float[2];
                for(int side=0;side<2;side++)
                {lengths[side*2]=Vector3.Distance(thighs[side].position,knees[side].position);lengths[side*2+1]=Vector3.Distance(knees[side].position,feet[side].position);before[side]=Sole(actor.Root,feet[side],out _);}
                actor.PosePhoto();
                for(int side=0;side<2;side++)
                {
                    float angle=Sole(actor.Root,feet[side],out float bottom);
                    float error=Mathf.Max(Mathf.Abs(Vector3.Distance(thighs[side].position,knees[side].position)-lengths[side*2]),
                        Mathf.Abs(Vector3.Distance(knees[side].position,feet[side].position)-lengths[side*2+1]));
                    report.AppendLine($"{id} side={side} soleTilt={before[side]:F3}->{angle:F3} bottom={bottom:F4} segmentError={error:F6}");
                    if(angle>12||Math.Abs(bottom)>.025f||error>.001f)throw new Exception("Photo sole is not planted: "+report);
                }
                UnityEngine.Object.DestroyImmediate(actor.Root.gameObject);
                using(var photo=new PhotoComposition(1920,1080,id))
                {
                    var shadow=photo.Hero.Root.parent.Find("Photo hero ground shadow").GetComponent<MeshRenderer>();
                    var mask=(RenderTexture)shadow.sharedMaterial.mainTexture;
                    if(mask.width!=512||mask.height>512)throw new Exception("Unbounded shadow target");
                    var previous=RenderTexture.active;var maskImage=new Texture2D(mask.width,mask.height,TextureFormat.RGB24,false,true);
                    try {RenderTexture.active=mask;maskImage.ReadPixels(new Rect(0,0,mask.width,mask.height),0,0);maskImage.Apply();File.WriteAllBytes(folder+"/"+id+"-mask.png",maskImage.EncodeToPNG());}
                    finally {RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(maskImage);}
                    // Keep identical renderer bounds for the directional
                    // shadow atlas; removing a receiver can change its fit.
                    photo.Render(true);
                    var on=photo.Snapshot();shadow.sharedMaterial.SetFloat("_Strength",0);var off=photo.Snapshot();
                    shadow.sharedMaterial.SetFloat("_Strength",1);var repeat=photo.Snapshot();
                    try
                    {
                        File.WriteAllBytes(folder+"/"+id+"-with-shadow.png",on.EncodeToPNG());File.WriteAllBytes(folder+"/"+id+"-no-shadow.png",off.EncodeToPNG());
                        var a=on.GetPixels32();var b=off.GetPixels32();var c=repeat.GetPixels32();int affected=0,outside=0,repeatError=0;long darkening=0;
                        for(int i=0;i<a.Length;i++)
                        {
                            int dark=(b[i].r-a[i].r)+(b[i].g-a[i].g)+(b[i].b-a[i].b);
                            if(dark>3){affected++;darkening+=dark;if(i/1920>230)outside++;}
                            repeatError=Math.Max(repeatError,Math.Abs(a[i].r-c[i].r)+Math.Abs(a[i].g-c[i].g)+Math.Abs(a[i].b-c[i].b));
                            if(dark< -3)throw new Exception("Shadow brightened backdrop");
                        }
                        report.AppendLine($"{id} shadowPixels={affected} darkening={darkening} outsideFloor={outside} repeatError={repeatError} mask={mask.width}x{mask.height}");
                        if(affected<100||outside>0||repeatError>3)throw new Exception("Photo shadow missing, drifting or covers upper body: "+report);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(on);UnityEngine.Object.DestroyImmediate(off);UnityEngine.Object.DestroyImmediate(repeat);}
                }
            }
            PhotoCompositionChecks.Run();
            File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[PhotoGroundingReview] PASS\n"+report);
        }
        static Transform Bone(Transform[] bones,string first,string second)=>bones.Single(b=>b.name==first||b.name==second);
        static float Sole(Transform root,Transform foot,out float bottom)
        {
            bottom=float.PositiveInfinity;Vector3 normal=Vector3.zero;var mesh=new Mesh();
            try
            {
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                    float W(int i,float value)=>(bones[i]==foot||bones[i].IsChildOf(foot))?value:0;
                    var selected=weights.Select(w=>W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)>.5f).ToArray();
                    skin.BakeMesh(mesh,true);var v=mesh.vertices.Select(p=>skin.transform.TransformPoint(p)).ToArray();var triangles=mesh.triangles;
                    for(int i=0;i<v.Length;i++)if(selected[i])bottom=Mathf.Min(bottom,v[i].y);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];if(!selected[a]||!selected[b]||!selected[c])continue;
                        var n=Vector3.Cross(v[b]-v[a],v[c]-v[a]);if(n.sqrMagnitude<.00000001f||n.normalized.y>-.70f)continue;
                        if((v[a].y+v[b].y+v[c].y)/3>foot.position.y-.015f)continue;normal+=n;
                    }
                }
                if(normal.sqrMagnitude<.000001f)throw new Exception("No measurable sole: "+foot.name);
                return Vector3.Angle(normal,Vector3.down);
            }
            finally {UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
