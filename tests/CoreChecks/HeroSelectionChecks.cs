using System;
using UltramanGame.Core;

static class HeroSelectionChecks
{
    static PoseFrame Frame(string kind,ref long stamp,ref long sequence)
    {
        stamp+=50;var p=new PosePoint[33];for(int i=0;i<p.Length;i++)p[i]=new PosePoint(.5f,.5f);
        p[11]=new PosePoint(.65f,.30f);p[12]=new PosePoint(.35f,.30f);
        p[13]=new PosePoint(.69f,.50f);p[14]=new PosePoint(.31f,.50f);
        p[15]=new PosePoint(.67f,.60f);p[16]=new PosePoint(.33f,.60f);
        if(kind=="left"){p[15]=new PosePoint(.90f,.02f);p[16]=new PosePoint(.33f,.60f);}
        if(kind=="right"){p[15]=new PosePoint(.67f,.60f);p[16]=new PosePoint(.10f,.02f);}
        if(kind=="both"){p[15]=new PosePoint(.90f,.02f);p[16]=new PosePoint(.10f,.02f);}
        return new PoseFrame{schema=1,streamId="selection",sequence=++sequence,capturedMs=stamp,tracked=true,points=p};
    }
    public static void Run(Action<bool,string> check)
    {
        var g=new HeroSelectionGesture();long stamp=10000,sequence=0;
        int left=0;for(int i=0;i<10;i++){var f=Frame("left",ref stamp,ref sequence);if(g.Update(f,stamp)<0)left++;}
        check(left==1,"single raised left hand selects previous hero once");
        for(int i=0;i<8;i++){var f=Frame("neutral",ref stamp,ref sequence);g.Update(f,stamp);}
        int right=0;for(int i=0;i<10;i++){var f=Frame("right",ref stamp,ref sequence);if(g.Update(f,stamp)>0)right++;}
        check(right==1,"single raised right hand selects next hero once");
        for(int i=0;i<8;i++){var f=Frame("neutral",ref stamp,ref sequence);g.Update(f,stamp);}
        for(int i=0;i<10;i++){var f=Frame("both",ref stamp,ref sequence);if(g.Update(f,stamp)!=0)throw new Exception("both hands selected a hero");}
        check(g.Armed,"both raised hands do not consume selection ownership");
        g.Consume();for(int i=0;i<10;i++){var f=Frame("left",ref stamp,ref sequence);if(g.Update(f,stamp)!=0)throw new Exception("held hand retriggered after consume");}
        for(int i=0;i<5;i++){var f=Frame("neutral",ref stamp,ref sequence);g.Update(f,stamp);}
        check(g.Armed,"hands-down release rearms selection after a change");
        var stale=Frame("right",ref stamp,ref sequence);check(g.Update(stale,stamp+400)==0,"stale selection pose is ignored");
    }
}
