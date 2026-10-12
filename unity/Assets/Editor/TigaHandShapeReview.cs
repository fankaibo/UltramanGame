using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UltramanGame.Editor
{
    public static class TigaHandShapeReview
    {
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../artifacts/tiga-hand-shape-20261012");
        public static void Inspect()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Directory.CreateDirectory(Folder);
            var prefab=Resources.Load<GameObject>("Characters/Tiga/Tiga");var report=new StringBuilder();
            foreach(var clip in Resources.LoadAll<AnimationClip>("Characters/Tiga/Tiga"))
            {
                string name=clip.name.Substring(clip.name.LastIndexOf('|')+1);
                if(name!="Idle"&&name!="LeftPunch"&&name!="RightPunch"&&name!="Guard"&&name!="Beam")continue;
                foreach(float fraction in new[]{0f,.25f,.50f,.75f,.999f})
                {
                    // A fresh rig per sample prevents an unkeyed channel from
                    // inheriting the preceding clip's (notably Beam's) open hand.
                    var model=UnityEngine.Object.Instantiate(prefab);
                    var joints=model.GetComponentsInChildren<Transform>();
                    clip.SampleAnimation(model,clip.length*fraction);
                    report.AppendLine("CLIP "+name+" fraction="+fraction+" length="+clip.length);
                    foreach(var joint in joints)
                    {
                        bool under=false;for(var parent=joint.parent;parent;parent=parent.parent)if(parent.name=="HandBase_L"||parent.name=="HandBase_R")under=true;
                        if(under)report.AppendLine(joint.name+" parent="+joint.parent.name+" pos="+joint.localPosition.ToString("F6")+" rotation="+joint.localRotation.ToString("F6"));
                    }
                    UnityEngine.Object.DestroyImmediate(model);
                }
            }
            File.WriteAllText(Folder+"/source-hand-clips.txt",report.ToString());Debug.Log("[TigaHandShape] inspected source clips");
        }
    }
}
