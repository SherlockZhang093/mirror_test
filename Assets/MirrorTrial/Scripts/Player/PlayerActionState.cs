namespace MirrorTrial.Player
{
    public enum PlayerActionState
    {
        None = 0,
        Idle = 1,
        Run = 2,
        JumpRise = 3,
        JumpFall = 4,
        Land = 5,
        Attack = 6,
        Cast = 7,
        Dash = 8,
        Hurt = 9,
        Dead = 10,
        // 弓
        BowDraw = 11,
        BowAim = 12,
        BowFull = 13,
        BowFire = 14,
        // 拳 - 连击
        ComboAttackA = 15,
        ComboAttackB = 16,
        ComboAttackC = 17,
        ComboAttackD = 18,
        // 拳 - 拳击
        PunchA = 19,
        PunchB = 20,
        PunchC = 21,
        // 拳 - 踢击
        KickA = 22,
        KickB = 23,
        KickC = 24,
        // 剑 - 额外攻击
        SwordStandingSlash = 25,
        SwordRunSlash = 26,
        SwordGuard = 27,
        SwordGuardImpact = 28,
        SwordSprintSlash = 29,
        CrouchSlash = 30,
        Dodge = 31,
        LedgeHang = 32,
        LedgeClimb = 33,
        MonkeyBarIdle = 34,
        AirSlashUp = 35,
        AirSlashDown = 36,

        // New traversal/ability states must stay appended so existing prefab
        // animation bindings keep their serialized numeric identity.
        DoubleJump = 37,
        LadderGrab = 38,
        LadderIdle = 39,
        LadderClimbUpLeft = 40,
        LadderClimbUpRight = 41,
        LadderClimbDownLeft = 42,
        LadderClimbDownRight = 43,
        LadderClimbFinish = 44,
        LadderJumpPrepare = 45,
        PunchD = 46
    }
}
