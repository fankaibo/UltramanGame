namespace UltramanGame.Core
{
    // Live ownership wins over the short-lived receipt of an earlier gesture.
    // This is presentation only: it never changes recognition or battle state.
    public static class CameraGuidance
    {
        public static string GuardInstruction(Battle battle,PlayerInput input,bool keyboard)
        {
            if(battle.Shield)return keyboard?"护盾已展开 · 继续按住 S":"护盾已展开 · 保持住！";
            if(keyboard)return "按住 S，展开护盾！";
            return input.GuardIntent?"看见防御姿势 · 保持一下":"双手交叉护在胸前或脸前";
        }

        public static string Status(Battle battle,GestureRecognizer gestures,PlayerInput input,bool present,string recent)
        {
            if(!present)return "请站进镜头";
            if(battle.Phase==GamePhase.Waiting)
                return gestures.TransformNeedsRelease?"先放下双手":
                    gestures.TransformProgress>0?"双手举高 · 保持":"看见你了 · 举高双手";
            if(battle.Phase==GamePhase.Transforming)return "变身成功";
            if(battle.Phase==GamePhase.Victory)return "守护成功";
            if(battle.Phase==GamePhase.Paused)return "看见你了 · 准备继续";
            if(battle.Action==HeroAction.Hurt)return "站稳，马上恢复";
            if(battle.Action==HeroAction.Beam)return "光线释放中";
            bool ready=battle.Energy>=Battle.MaxEnergy;
            if(ready&&!gestures.BeamNeedsRelease&&(gestures.BeamProgress>0||input.BeamIntent))
                return "光线姿势 · 保持";
            bool warning=battle.Enemy==EnemyPhase.Windup||battle.Enemy==EnemyPhase.Attack;
            if(ready&&gestures.BeamNeedsRelease&&!warning)return "先收手，再摆光线姿势";
            if(battle.Shield)return "护盾已展开";
            if(input.GuardIntent)return "防御姿势 · 保持";
            // A released shield cannot continue to claim that it is active.
            return !string.IsNullOrEmpty(recent)&&recent!="护盾已展开"?recent:"看见你了";
        }
    }
}
