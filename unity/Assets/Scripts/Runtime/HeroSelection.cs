using UnityEngine;
using UltramanGame.Core;
namespace UltramanGame.Runtime
{
    public sealed partial class ArenaController
    {
        int heroIndex;
        HeroDefinition SelectedHero=>HeroRoster.At(heroIndex);
        string selectionHint="";
        float selectionChangedAt;
        static bool HeroAvailable(int index)
        {var id=HeroRoster.At(index).Id;return Resources.Load<GameObject>("Characters/"+id+"/"+id)!=null;}
        void SelectAdjacent(int direction)
        {
            direction=direction<0?-1:1;
            // Keep the carousel usable while Zeta/Decker are still waiting
            // for their real model files.  A future resource becomes
            // selectable automatically without changing the roster order.
            for(int step=1;step<=HeroRoster.Count;step++)
            {
                int candidate=HeroRoster.Wrap(heroIndex+direction*step);
                if(HeroAvailable(candidate)){SelectHero(candidate);return;}
            }
            selectionHint="暂无可用英雄";
        }
        void SelectHero(int index)
        {
            if(battle.Phase!=GamePhase.Waiting||photo.Active||settings||showcase)return;
            index=HeroRoster.Wrap(index);
            if(!HeroAvailable(index)){selectionHint=HeroRoster.At(index).Name+"形象准备中";return;}
            if(index==heroIndex)return;
            // Construct first so a bad resource cannot destroy the currently playable hero.
            var replacement=new AnimatedActor(HeroRoster.At(index).Id,world.HeroHome,world.EnemyHome);
            var old=hero;hero=replacement;heroIndex=index;world.BindActors(hero,enemy);Destroy(old.Root.gameObject);
            photo.HeroId=SelectedHero.Id;sound.HeroId=SelectedHero.Id;world.SetHeroProfile(SelectedHero.Id);
            PlayerPrefs.SetString("hero.selected",SelectedHero.Id);PlayerPrefs.Save();
            recognizer.Reset();held=default;selectionHint="";selectionChangedAt=Time.unscaledTime;
            heroSelectionGesture.Consume();
            hero.Update(battle,world.Camera,0,Time.unscaledTime);
            sound.Effect("shield",.25f);sound.Speak("hero_"+SelectedHero.Id.ToLowerInvariant(),6,GamePhase.Waiting);
            Debug.Log($"[HeroSelection] id={SelectedHero.Id} name={SelectedHero.Name} rigged={hero.IsRigged}");
        }
        void DrawHeroSelection()
        {
            if(battle.Phase!=GamePhase.Waiting)return;
            const float x=319,y=510,w=122,gap=10;
            hud.Text(new Rect(x,475,642,24),selectionHint.Length>0?selectionHint:"举左手/右手切换英雄 · 双手举高开始",17,HudPainter.Cyan,TextAnchor.MiddleCenter);
            hud.Text(new Rect(x,499,642,18),SelectedHero.Name+" · "+SelectedHero.Form+" · "+SelectedHero.Beam,11,HudPainter.Muted,TextAnchor.MiddleCenter);
            for(int i=0;i<HeroRoster.Count;i++)
            {
                var r=new Rect(x+i*(w+gap),y,w,85);bool selected=i==heroIndex,available=HeroAvailable(i);
                hud.Panel(r,selected?HudPainter.Gold:HudPainter.Cyan,selected);
                var portrait=Resources.Load<Texture2D>("Characters/"+HeroRoster.At(i).Id+"/Photo");
                if(portrait)hud.Image(new Rect(r.x+4,r.y+3,43,58),portrait,ScaleMode.ScaleToFit);
                else if(i==0)hud.Portrait(new Rect(r.x+4,r.y+3,43,58),false);
                hud.Text(new Rect(r.x+42,r.y+7,76,27),HeroRoster.At(i).Name,15,available?HudPainter.Ink:HudPainter.Muted,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(r.x+4,r.y+56,w-8,23),selected?"已选择":available?"选择":"准备中",12,selected?HudPainter.Gold:HudPainter.Muted,TextAnchor.MiddleCenter);
                if(GUI.Button(r,GUIContent.none,GUIStyle.none))SelectHero(i);
            }
        }
    }
}
