using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class PhotoCompositionChecks
    {
        public static void Run()
        {
            for(int destructionOrder=0;destructionOrder<3;destructionOrder++)
            {
                var composition=new PhotoComposition();
                var meshes=composition.Hero.Root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Where(m=>!EditorUtility.IsPersistent(m)).ToArray();
                var materials=composition.Hero.Root.parent.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                var masks=materials.Select(m=>m.mainTexture).OfType<RenderTexture>().ToArray();
                var lamps=composition.Hero.Root.parent.GetComponentsInChildren<Light>();
                try
                {
                    if(destructionOrder>0)
                    {
                        Camera owner=null;
                        foreach(var candidate in Resources.FindObjectsOfTypeAll<Camera>())
                            if(candidate.targetTexture==composition.Preview){owner=candidate;break;}
                        if(!owner)throw new Exception("Photo render target has no camera");
                        UnityEngine.Object.DestroyImmediate(destructionOrder==1?owner.gameObject:owner.transform.parent.gameObject);
                    }
                    composition.Dispose();composition.Dispose();
                    if(composition.Preview)throw new Exception("Photo render target leaked during disposal");
                    if(meshes.Any(m=>m)||materials.Any(m=>m)||lamps.Any(l=>l)||masks.Any(m=>m))
                        throw new Exception("Photo resource leak, order="+destructionOrder+" meshes="+string.Join(",",meshes.Where(m=>m).Select(m=>m.name))+
                            " materials="+string.Join(",",materials.Where(m=>m).Select(m=>m.name))+" lights="+string.Join(",",lamps.Where(l=>l).Select(l=>l.name)));
                }
                finally {composition.Dispose();}
            }
            Debug.Log("[PhotoCleanupChecks] PASS normal, camera-first, root-first destruction and repeated disposal; meshes, materials and lights released");
        }
    }
}
