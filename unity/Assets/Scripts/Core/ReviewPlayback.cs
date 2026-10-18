namespace UltramanGame.Core
{
    // Opt-in development playback. It sends normal inputs, never edits health,
    // energy, clocks or recognition thresholds. No camera is opened by this mode.
    public sealed class ReviewPlayback
    {
        public float Age {get;private set;}
        public bool Interrupted {get;private set;}
        float nextPunch,lossAge=-1;
        bool alternate;
        readonly bool groundSlam,headRay,linkedPunches,clawCycle;
        public bool GuardHandoffEnabled {get;}
        public bool GuardHandoffObserved=>guardStage==2;
        int guardStage;float guardAt;bool counterLeft;
        int queuedFor;
        public ReviewPlayback(bool groundSlam=false,bool headRay=false,bool linkedPunches=false,bool guardHandoff=false,bool clawCycle=false)
        {this.clawCycle=clawCycle;this.groundSlam=groundSlam;this.headRay=headRay;this.linkedPunches=linkedPunches;GuardHandoffEnabled=guardHandoff;}
        public PlayerInput Next(Battle battle,float dt)
        {
            Age+=dt;var input=new PlayerInput{Tracking=true};
            if(battle.Phase==GamePhase.Waiting){input.Transform=Age>1;return input;}
            if(battle.Phase!=GamePhase.Battle&&battle.Phase!=GamePhase.Paused)return input;
            if(!Interrupted&&battle.Punches>=8){Interrupted=true;lossAge=0;}
            if(lossAge>=0&&lossAge<2){lossAge+=dt;input.Tracking=false;return input;}
            if(battle.Phase==GamePhase.Paused)return input;
            // First take one harmless hit, then demonstrate a successful block.
            if(battle.HitsTaken==0)return input;
            if(battle.Blocks<(clawCycle?4:headRay?3:groundSlam?2:1)){input.Shield=true;return input;}
            if(GuardHandoffEnabled&&guardStage<2)
            {
                if(guardStage==0&&battle.Punches==1&&battle.IsPunch&&battle.ActionAge>=.14f)
                {guardStage=1;guardAt=Age;counterLeft=battle.Action==HeroAction.RightPunch;}
                if(guardStage==1)
                {
                    if(Age-guardAt<.30f){input.Shield=input.GuardIntent=true;return input;}
                    guardStage=2;input.LeftPunch=counterLeft;input.RightPunch=!counterLeft;nextPunch=Age+.68f;return input;
                }
            }
            if(battle.Enemy==EnemyPhase.Attack){input.Shield=true;return input;}
            if(linkedPunches&&battle.Punches>0&&battle.Punches<4&&queuedFor!=battle.Punches&&battle.ActionAge>=.24f&&
                (battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch))
            {
                queuedFor=battle.Punches;alternate=battle.Action==HeroAction.RightPunch;
                input.LeftPunch=alternate;input.RightPunch=!alternate;nextPunch=Age+.68f;return input;
            }
            if(battle.Action!=HeroAction.None)return input;
            if(battle.Energy>=Battle.MaxEnergy){input.Beam=true;return input;}
            if(Age>=nextPunch)
            {nextPunch=Age+.68f;alternate=!alternate;input.LeftPunch=alternate;input.RightPunch=!alternate;}
            return input;
        }
    }
}
