using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed partial class ArenaController
    {
        readonly HashSet<string> proofFrames=new HashSet<string>();
        bool proofBusy;
        void CaptureGuidedProof()
        {
            if(!Debug.isDebugBuild||pose?.source!="synthetic"||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--guided-proof")<0||proofBusy)return;
            string key=photo.Active?"photo-"+photo.Stage+(photo.Stage==PhotoStage.Framing?(photo.HasLivePerson?"-live":"-preparing"):""):
                battle.Phase==GamePhase.Battle?(world.Closeup.Focus>.999f?"beam-closeup-peak":world.Closeup.Active?"beam-closeup":world.BeamVisible?"beam-firing":battle.Enemy==EnemyPhase.Attack?"monster-rush":battle.Enemy==EnemyPhase.Windup?"guard-guide":battle.Energy>=15?"beam-guide":battle.Punches>2?"battle":"battle-entry"):
                battle.Phase.ToString();
            if(proofFrames.Contains(key))return;
            proofFrames.Add(key);StartCoroutine(SaveGuidedProof(key));
        }
        IEnumerator SaveGuidedProof(string key)
        {
            proofBusy=true;yield return new WaitForEndOfFrame();
            var args=System.Environment.GetCommandLineArgs();int at=System.Array.IndexOf(args,"--proof-output");
            string folder=at>=0&&at+1<args.Length?Path.GetFullPath(args[at+1]):Path.Combine(System.Environment.CurrentDirectory,"artifacts/guided-arcade/native");Directory.CreateDirectory(folder);
            var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(folder,key+".png"),texture.EncodeToPNG());Destroy(texture);proofBusy=false;
            Debug.Log("[GuidedProof] screenshot="+key+" source=synthetic file="+Path.Combine(folder,key+".png"));
        }
    }
}
