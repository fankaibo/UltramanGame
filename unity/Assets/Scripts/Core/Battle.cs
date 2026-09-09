using System;
using System.Collections.Generic;

namespace UltramanGame.Core
{
    public enum GamePhase { Waiting, Transforming, Battle, Paused, Victory }
    public enum EnemyPhase { Rest, Windup, Attack, Recover }
    public enum HeroAction { None, LeftPunch, RightPunch, Beam, Hurt }
    public enum GameCue { Transform, BattleStart, Warning, Punch, Beam, Block, Hurt, EnergyReady, Victory, Resume, EnemyAttack }

    // Unity-free deterministic rules. The renderer observes state; it never awards damage.
    public sealed class Battle
    {
        public GamePhase Phase { get; private set; } = GamePhase.Waiting;
        public EnemyPhase Enemy { get; private set; }
        public HeroAction Action { get; private set; }
        public int MaxHealth { get; }
        public float EnemyHealth { get; private set; }
        public float Energy { get; private set; }
        public float ActionAge { get; private set; }
        public float EnemyAge { get; private set; }
        public float WarningDuration { get; private set; } = WindupSeconds;
        public float InstructionRemaining { get; private set; }
        public float ResumeProgress { get; private set; }
        public bool Shield { get; private set; }
        public int Punches { get; private set; }
        public int Blocks { get; private set; }
        public int HitsTaken { get; private set; }
        public const int DefaultMonsterHits=50, MinMonsterHits=10, MaxMonsterHits=200, MaxEnergy=15;
        public const float InstructionReactionSeconds=3f, WindupSeconds=5.4f;
        public const float EnemyHitSeconds=.4f, EnemyAttackSeconds=1.05f;
        public const float PunchSeconds=.38f, PunchHitSeconds=.12f;
        readonly Queue<GameCue> cues=new Queue<GameCue>();
        GamePhase resumePhase;
        float phaseAge, immunity;
        bool hitApplied;
        bool enemyHitApplied;
        HeroAction queuedPunch;
        float queuedAge;
        float queuedBeamAge;

        public Battle(int monsterHits=DefaultMonsterHits)
        { MaxHealth=ClampMonsterHits(monsterHits);EnemyHealth=MaxHealth; }
        public static int ClampMonsterHits(int value)
        { return Math.Max(MinMonsterHits,Math.Min(MaxMonsterHits,value)); }

        public bool TryCue(out GameCue cue)
        { if(cues.Count==0) { cue=default; return false; } cue=cues.Dequeue(); return true; }
        void Cue(GameCue cue) { if(cues.Count<16) cues.Enqueue(cue); }
        // Called when a guide line actually starts, including lines that waited in the audio queue.
        // Only the enemy waits: the child may attack or release the beam immediately.
        public void GiveInstructionTime(float voiceSeconds,bool warning=false)
        {
            if(float.IsNaN(voiceSeconds)||float.IsInfinity(voiceSeconds)||voiceSeconds<0)
                throw new ArgumentOutOfRangeException(nameof(voiceSeconds));
            if(Phase!=GamePhase.Battle)return;
            float duration=Math.Min(voiceSeconds,20)+InstructionReactionSeconds;
            if(warning)
            {
                if(Enemy==EnemyPhase.Windup)WarningDuration=Math.Max(WarningDuration,EnemyAge+duration);
                return;
            }
            InstructionRemaining=Math.Max(InstructionRemaining,duration);
            // A new action lesson replaces an unfinished warning/rush instead of freezing it midair.
            Enemy=EnemyPhase.Rest;EnemyAge=0;enemyHitApplied=false;
        }
        public void Pause()
        {
            if(Phase==GamePhase.Paused || Phase==GamePhase.Waiting || Phase==GamePhase.Victory) return;
            resumePhase=Phase; Phase=GamePhase.Paused; ResumeProgress=0;
            Action=HeroAction.None; Shield=false; Enemy=EnemyPhase.Rest; EnemyAge=0;
            queuedPunch=HeroAction.None;queuedAge=0;
            queuedBeamAge=0;InstructionRemaining=0;WarningDuration=WindupSeconds;
            enemyHitApplied=false;
            cues.Clear();
        }
        public void Tick(float dt,PlayerInput input)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0) throw new ArgumentOutOfRangeException(nameof(dt));
            dt=Math.Min(dt,.1f); // avoid one stalled render frame skipping an entire warning
            if(Phase==GamePhase.Victory) return;
            if(!input.Tracking) { Pause(); ResumeProgress=0; return; }
            if(Phase==GamePhase.Paused)
            {
                ResumeProgress+=dt;
                if(ResumeProgress>=1.2f) { Phase=resumePhase; immunity=1;GiveInstructionTime(0); Cue(GameCue.Resume); }
                return;
            }
            phaseAge+=dt;
            if(Phase==GamePhase.Waiting)
            {
                if(input.Transform) { Phase=GamePhase.Transforming; phaseAge=0; Cue(GameCue.Transform); }
                return;
            }
            if(Phase==GamePhase.Transforming)
            {
                if(phaseAge>=2.2f) { Phase=GamePhase.Battle; phaseAge=0;GiveInstructionTime(0); Cue(GameCue.BattleStart); }
                return;
            }
            immunity=Math.Max(0,immunity-dt);
            InstructionRemaining=Math.Max(0,InstructionRemaining-dt);
            queuedBeamAge=Math.Max(0,queuedBeamAge-dt);
            if(input.Beam&&Energy>=MaxEnergy&&Action!=HeroAction.Beam)queuedBeamAge=.8f;
            queuedAge-=dt;
            if(queuedAge<=0 || input.Shield || queuedBeamAge>0) queuedPunch=HeroAction.None;
            if((Action==HeroAction.LeftPunch || Action==HeroAction.RightPunch) &&
                ActionAge>=PunchSeconds-.18f && !input.Shield && !input.Beam && (input.LeftPunch || input.RightPunch))
            { queuedPunch=input.LeftPunch?HeroAction.LeftPunch:HeroAction.RightPunch;queuedAge=.20f; }
            Shield=input.Shield && (Action==HeroAction.None);
            if(Action==HeroAction.None)
            {
                if(queuedBeamAge>0 && Energy>=MaxEnergy)
                {
                    queuedBeamAge=0;InstructionRemaining=0;
                    Energy=0;Begin(HeroAction.Beam);Shield=false;
                    Enemy=EnemyPhase.Rest;EnemyAge=0;enemyHitApplied=false;Cue(GameCue.Beam);
                }
                else if(!Shield && (input.LeftPunch || input.RightPunch || queuedPunch!=HeroAction.None))
                {
                    var next=input.LeftPunch?HeroAction.LeftPunch:input.RightPunch?HeroAction.RightPunch:queuedPunch;
                    queuedPunch=HeroAction.None;Begin(next);Cue(GameCue.Punch);
                }
            }
            if(Action!=HeroAction.None)
            {
                ActionAge+=dt;
                float hitTime=Action==HeroAction.Beam?.45f:PunchHitSeconds;
                if(!hitApplied && Action!=HeroAction.Hurt && ActionAge>=hitTime)
                {
                    hitApplied=true;
                    if(Action==HeroAction.Beam) EnemyHealth=Math.Max(0,EnemyHealth-9);
                    else { EnemyHealth=Math.Max(0,EnemyHealth-1); Punches++; AddEnergy(1); }
                    if(EnemyHealth<=0) { Phase=GamePhase.Victory; Shield=false; Cue(GameCue.Victory); return; }
                }
                float duration=Action==HeroAction.Beam?1.5f:Action==HeroAction.Hurt?.55f:PunchSeconds;
                if(ActionAge>=duration) Action=HeroAction.None;
            }
            // Special move provides an obvious window of protection.
            if(Action==HeroAction.Beam || InstructionRemaining>0) return;
            EnemyAge+=dt;
            if(Enemy==EnemyPhase.Rest && EnemyAge>=4)
            { Enemy=EnemyPhase.Windup; EnemyAge=0;WarningDuration=WindupSeconds; Cue(GameCue.Warning); }
            else if(Enemy==EnemyPhase.Windup && EnemyAge>=WarningDuration)
            {
                Enemy=EnemyPhase.Attack;EnemyAge=0;enemyHitApplied=false;Cue(GameCue.EnemyAttack);
            }
            else if(Enemy==EnemyPhase.Attack)
            {
                if(!enemyHitApplied&&EnemyAge>=EnemyHitSeconds)
                {
                    enemyHitApplied=true;
                    if(Shield) {Blocks++;Cue(GameCue.Block);}
                    else if(immunity<=0) {HitsTaken++;immunity=2;queuedPunch=HeroAction.None;Begin(HeroAction.Hurt);Cue(GameCue.Hurt);}
                }
                if(EnemyAge>=EnemyAttackSeconds) {Enemy=EnemyPhase.Recover;EnemyAge=0;}
            }
            else if(Enemy==EnemyPhase.Recover && EnemyAge>=2)
            { Enemy=EnemyPhase.Rest; EnemyAge=0; }
        }
        void Begin(HeroAction action) { Action=action; ActionAge=0; hitApplied=false; }
        void AddEnergy(float amount)
        {
            float before=Energy;
            Energy=Math.Min(MaxEnergy,Energy+amount);
            if(before<MaxEnergy && Energy>=MaxEnergy) {GiveInstructionTime(0);Cue(GameCue.EnergyReady);}
        }
    }
}
