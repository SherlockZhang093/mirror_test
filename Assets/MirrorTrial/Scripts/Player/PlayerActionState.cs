namespace MirrorTrial.Player
{
    public enum PlayerActionState
    {
        None,
        Idle,
        Run,
        JumpRise,
        JumpFall,
        Land,
        Attack,
        Cast,
        Dash,
        Hurt,
        Dead,
        // 弓
        BowDraw,
        BowAim,
        BowFull,
        BowFire,
        // 拳 - 连击
        ComboAttackA,
        ComboAttackB,
        ComboAttackC,
        ComboAttackD,
        // 拳 - 拳击
        PunchA,
        PunchB,
        PunchC,
        // 拳 - 踢击
        KickA,
        KickB,
        KickC,
        // 剑 - 额外攻击
        SwordStandingSlash,
        SwordRunSlash,
        SwordGuard,
        SwordGuardImpact,
        SwordSprintSlash,
        CrouchSlash,
        Dodge
    }
}
