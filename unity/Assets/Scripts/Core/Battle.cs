using System;
using System.Collections.Generic;

namespace UltramanGame.Core
{
    public enum GamePhase { Waiting, Transforming, Battle, Paused, Victory }
    public enum EnemyPhase { Rest, Windup, Recover }
    public enum HeroAction { None, LeftPunch, RightPunch, Beam, Hurt }
    public enum GameCue { Transform, BattleStart, Warning, Punch, Beam, Block, Hurt, EnergyReady, Victory, Resume }

    // Unity-free deterministic rules. The renderer observes state; it never awards damage.
    public sealed class Battle
    {
        public GamePhase Phase { get; private set; } = GamePhase.Waiting;
        public EnemyPhase Enemy { get; private set; }
        public HeroAction Action { get; private set; }
        public float EnemyHealth { get; private set; } = 24;
        public float Energy { get; private set; }
        public float ActionAge { get; private set; }
        public float EnemyAge { get; private set; }
        public float ResumeProgress { get; private set; }
        public bool Shield { get; private set; }
        public int Punches { get; private set; }
        public int Blocks { get; private set; }
        public int HitsTaken { get; private set; }
        public const float MaxHealth=24, MaxEnergy=6, WindupSeconds=2.4f;
        readonly Queue<GameCue> cues=new Queue<GameCue>();
        GamePhase resumePhase;
        float phaseAge, immunity;
        bool hitApplied;

        public bool TryCue(out GameCue cue)
        { if(cues.Count==0) { cue=default; return false; } cue=cues.Dequeue(); return true; }
        void Cue(GameCue cue) { if(cues.Count<16) cues.Enqueue(cue); }
        public void Pause()
        {
            if(Phase==GamePhase.Paused || Phase==GamePhase.Waiting || Phase==GamePhase.Victory) return;
            resumePhase=Phase; Phase=GamePhase.Paused; ResumeProgress=0;
            Action=HeroAction.None; Shield=false; Enemy=EnemyPhase.Rest; EnemyAge=0;
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
                if(ResumeProgress>=1.2f) { Phase=resumePhase; immunity=1; Cue(GameCue.Resume); }
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
                if(phaseAge>=2.2f) { Phase=GamePhase.Battle; phaseAge=0; Cue(GameCue.BattleStart); }
                return;
            }
            immunity=Math.Max(0,immunity-dt);
            Shield=input.Shield && (Action==HeroAction.None);
            if(Action==HeroAction.None)
            {
                if(input.Beam && Energy>=MaxEnergy) { Energy=0; Begin(HeroAction.Beam); Shield=false; Cue(GameCue.Beam); }
                else if(!Shield && (input.LeftPunch || input.RightPunch))
                { Begin(input.LeftPunch?HeroAction.LeftPunch:HeroAction.RightPunch); Cue(GameCue.Punch); }
            }
            if(Action!=HeroAction.None)
            {
                ActionAge+=dt;
                float hitTime=Action==HeroAction.Beam?.45f:.18f;
                if(!hitApplied && Action!=HeroAction.Hurt && ActionAge>=hitTime)
                {
                    hitApplied=true;
                    if(Action==HeroAction.Beam) EnemyHealth=Math.Max(0,EnemyHealth-9);
                    else { EnemyHealth=Math.Max(0,EnemyHealth-1); Punches++; AddEnergy(1); }
                    if(EnemyHealth<=0) { Phase=GamePhase.Victory; Shield=false; Cue(GameCue.Victory); return; }
                }
                float duration=Action==HeroAction.Beam?1.5f:Action==HeroAction.Hurt?.55f:.45f;
                if(ActionAge>=duration) Action=HeroAction.None;
            }
            // Special move provides an obvious window of protection.
            if(Action==HeroAction.Beam) return;
            EnemyAge+=dt;
            if(Enemy==EnemyPhase.Rest && EnemyAge>=4)
            { Enemy=EnemyPhase.Windup; EnemyAge=0; Cue(GameCue.Warning); }
            else if(Enemy==EnemyPhase.Windup && EnemyAge>=WindupSeconds)
            {
                Enemy=EnemyPhase.Recover; EnemyAge=0;
                if(Shield) { Blocks++; AddEnergy(1); Cue(GameCue.Block); }
                else if(immunity<=0) { HitsTaken++; immunity=2; Begin(HeroAction.Hurt); Cue(GameCue.Hurt); }
            }
            else if(Enemy==EnemyPhase.Recover && EnemyAge>=2)
            { Enemy=EnemyPhase.Rest; EnemyAge=0; }
        }
        void Begin(HeroAction action) { Action=action; ActionAge=0; hitApplied=false; }
        void AddEnergy(float amount)
        {
            float before=Energy;
            Energy=Math.Min(MaxEnergy,Energy+amount);
            if(before<MaxEnergy && Energy>=MaxEnergy) Cue(GameCue.EnergyReady);
        }
    }
}
