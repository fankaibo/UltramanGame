using System;
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
                }
                finally {composition.Dispose();}
            }
            Debug.Log("[PhotoCleanupChecks] PASS normal, camera-first, root-first destruction and repeated disposal");
        }
    }
}
