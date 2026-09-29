using System;
using UltramanGame.Core;

static class ClawReactionChecks
{
    public static void Run(Action<bool,string> check)
    {
        bool mirrored=true;
        for(int i=0;i<60;i++)
        {
            float t=i/60f;
            var l=MonsterClawMotion.Sample(true,t,1,false,10,10);
            var r=MonsterClawMotion.Sample(false,t,-1,false,10,10);
            mirrored&=Math.Abs(l.X+r.X)<.0001f&&Math.Abs(l.Y-r.Y)<.0001f&&Math.Abs(l.Z-r.Z)<.0001f;
        }
        check(mirrored,"opposite punches exchange the struck and supporting monster arms");
        foreach(int rate in new[]{15,30,60})
        {
            bool bounded=true,ended=true;float peak=0;
            foreach(string mode in new[]{"punch","beam","launch"})foreach(bool left in new[]{false,true})
            for(int i=0;i<=rate*3;i++)
            {
                float t=(float)i/rate;
                var p=MonsterClawMotion.Sample(left,t,-1,true,mode=="launch"?t:10,mode=="beam"?t:10);
                bounded&=!float.IsNaN(p.Length)&&p.Length<.7f;peak=Math.Max(peak,p.Length);
                if(t==0||t>=2)ended&=p.Length==0;
            }
            check(bounded&&ended&&peak>.4f,$"{rate} Hz claw impulses stay bounded, begin at rest and return fully");
        }
        var before=MonsterClawMotion.Sample(true,.8f,1,false,10,.8f);
        var following=MonsterClawMotion.Sample(true,0,-1,true,10,.8f);
        check(before.Length>.3f&&before.X==following.X&&before.Y==following.Y&&before.Z==following.Z,
            "a following ordinary punch cannot restart or erase the ongoing beam brace");
        var airborne=MonsterClawMotion.Sample(true,.8f,1,false,.3f,10);
        check(airborne.Length>.4f,"airborne claws remain active after the ordinary recoil window");
        check(MonsterClawMotion.Carry(0)==1&&MonsterClawMotion.Carry(.09f)>.4f&&MonsterClawMotion.Carry(.18f)==0,
            "rapid contacts retain their current hand offset before releasing old momentum");
    }
}
