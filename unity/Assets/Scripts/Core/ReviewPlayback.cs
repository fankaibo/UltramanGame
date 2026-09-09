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
            if(battle.Blocks==0){input.Shield=true;return input;}
            if(battle.Enemy==EnemyPhase.Attack){input.Shield=true;return input;}
            if(battle.Action!=HeroAction.None)return input;
            if(battle.Energy>=Battle.MaxEnergy){input.Beam=true;return input;}
            if(Age>=nextPunch)
            {nextPunch=Age+.68f;alternate=!alternate;input.LeftPunch=alternate;input.RightPunch=!alternate;}
            return input;
        }
    }
}
