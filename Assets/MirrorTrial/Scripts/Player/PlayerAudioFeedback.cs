using MirrorTrial.Audio;
using MirrorTrial.Combat;
using MirrorTrial.HealthResources;
using MirrorTrial.Level;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace MirrorTrial.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerCombat), typeof(AudioSource))]
    public sealed class PlayerAudioFeedback : MonoBehaviour
    {
        [Header("Library")]
        [SerializeField] GameAudioPalette palette;

        [Header("Preconfigured Sources")]
        [SerializeField] AudioSource oneShotSource;
        [FormerlySerializedAs("runLoopSource")]
        [SerializeField] AudioSource footstepSource;
        [SerializeField] AudioSource ambienceSource;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] float footstepVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] float movementVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float combatVolume = 0.72f;
        [SerializeField, Range(0f, 1f)] float reactionVolume = 0.72f;
        [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.16f;

        [Header("Footstep Timing")]
        [SerializeField, Min(0f)] float minimumRunSpeed = 0.35f;
        [SerializeField, Min(0.05f)] float slowFootstepInterval = 0.46f;
        [SerializeField, Min(0.05f)] float fastFootstepInterval = 0.27f;

        static PlayerAudioFeedback instance;

        PlayerMotor motor;
        PlayerCombat combat;
        PlayerDodgeController dodgeController;
        PlayerBowCombat bowCombat;
        PlayerAbilityLoadout abilities;
        PlayerDamageReceiver damageReceiver;
        PlayerRecoveryAbility recovery;
        PlayerMoveCategory lastAttackCategory = PlayerMoveCategory.Sword;
        float nextFootstepAt;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            combat = GetComponent<PlayerCombat>();
            dodgeController = GetComponent<PlayerDodgeController>();
            bowCombat = GetComponent<PlayerBowCombat>();
            abilities = GetComponent<PlayerAbilityLoadout>();
            damageReceiver = GetComponent<PlayerDamageReceiver>();
            recovery = GetComponent<PlayerRecoveryAbility>();
            if (!palette) palette = GameAudioPalette.LoadDefault();

            if (!palette || !oneShotSource || !footstepSource || oneShotSource == footstepSource)
            {
                Debug.LogError("Player Audio Feedback needs its palette and two different preconfigured Audio Sources.", this);
                enabled = false;
                return;
            }

            ConfigureOneShotSource(oneShotSource);
            ConfigureOneShotSource(footstepSource);
            if (ambienceSource)
            {
                ambienceSource.playOnAwake = false;
                ambienceSource.loop = true;
                ambienceSource.spatialBlend = 0f;
                ambienceSource.dopplerLevel = 0f;
            }
            ConfigureAmbience(SceneManager.GetActiveScene());
        }

        void OnEnable()
        {
            instance = this;
            if (motor)
            {
                motor.Jumped += PlayJump;
                motor.Landed += PlayLand;
            }
            if (combat)
            {
                combat.AttackActivated += PlayAttack;
                combat.Blocked += PlayGuardClash;
            }
            if (dodgeController) dodgeController.DodgeStarted += PlayDodge;
            if (bowCombat)
            {
                bowCombat.DrawStarted += PlayBowDraw;
                bowCombat.ArrowReleased += PlayBowFire;
            }
            if (abilities) abilities.MirrorBladeReleased += PlayMirrorBlade;
            if (damageReceiver)
            {
                damageReceiver.Damaged += PlayHurt;
                damageReceiver.Died += PlayDeath;
            }
            if (recovery) recovery.HealCommitted += PlayHeal;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            if (instance == this) instance = null;
            if (motor)
            {
                motor.Jumped -= PlayJump;
                motor.Landed -= PlayLand;
            }
            if (combat)
            {
                combat.AttackActivated -= PlayAttack;
                combat.Blocked -= PlayGuardClash;
            }
            if (dodgeController) dodgeController.DodgeStarted -= PlayDodge;
            if (bowCombat)
            {
                bowCombat.DrawStarted -= PlayBowDraw;
                bowCombat.ArrowReleased -= PlayBowFire;
            }
            if (abilities) abilities.MirrorBladeReleased -= PlayMirrorBlade;
            if (damageReceiver)
            {
                damageReceiver.Damaged -= PlayHurt;
                damageReceiver.Died -= PlayDeath;
            }
            if (recovery) recovery.HealCommitted -= PlayHeal;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (ambienceSource) ambienceSource.Stop();
        }

        void Update()
        {
            if (!motor || !footstepSource || !palette)
                return;

            var speed = Mathf.Abs(motor.Velocity.x);
            var shouldStep = motor.StableGrounded && !motor.TraversalLocked && speed >= minimumRunSpeed;
            if (!shouldStep)
            {
                nextFootstepAt = Time.time;
                return;
            }

            if (Time.time < nextFootstepAt)
                return;

            var speedRatio = Mathf.Clamp01(speed / 5f);
            nextFootstepAt = Time.time + Mathf.Lerp(slowFootstepInterval, fastFootstepInterval, speedRatio);
            var footstepClip = ResolveFootstepClip();
            Play(footstepSource, footstepClip, footstepVolume, palette, ResolveFootstepVolume());
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ConfigureAmbience(scene);

        void ConfigureAmbience(Scene scene)
        {
            if (!ambienceSource || !palette)
                return;

            AudioClip clip = null;
            var audioVolume = 1f;
            if (scene.name.IndexOf("Mirror", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                clip = palette.mirrorAmbience;
                audioVolume = palette.mirrorAmbienceVolume;
            }
            else if (scene.name.IndexOf("Reality_02", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                clip = palette.jungleAmbience;
                audioVolume = palette.jungleAmbienceVolume;
            }
            else if (scene.name.IndexOf("Reality_01", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     scene.name.IndexOf("level_01", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     scene.name.IndexOf("TestGym", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                clip = palette.ruinsAmbience;
                audioVolume = palette.ruinsAmbienceVolume;
            }

            if (ambienceSource.clip == clip && ambienceSource.isPlaying)
                return;
            ambienceSource.Stop();
            ambienceSource.clip = clip;
            ambienceSource.volume = palette.ScaleVolume(ambienceVolume, audioVolume);
            if (clip) ambienceSource.Play();
        }

        AudioClip ResolveFootstepClip()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            return sceneName.IndexOf("Reality_02", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? palette.stoneFootstep
                : palette.sandFootstep;
        }

        float ResolveFootstepVolume()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            return sceneName.IndexOf("Reality_02", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? palette.stoneFootstepVolume
                : palette.sandFootstepVolume;
        }

        static void ConfigureOneShotSource(AudioSource source)
        {
            source.clip = null;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }

        static void Play(AudioSource source, AudioClip clip, float volume, GameAudioPalette volumePalette, float audioVolume)
        {
            if (source && clip)
                source.PlayOneShot(clip, volumePalette ? volumePalette.ScaleVolume(volume, audioVolume) : Mathf.Clamp01(volume) * Mathf.Clamp01(audioVolume));
        }

        void PlayJump() => Play(oneShotSource, palette.jump, movementVolume, palette, palette.jumpVolume);
        void PlayLand(float impactSpeed) => Play(oneShotSource, palette.land, movementVolume * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(impactSpeed / 8f)), palette, palette.landVolume);
        void PlayDodge() => Play(oneShotSource, palette.dodge, movementVolume, palette, palette.dodgeVolume);

        void PlayAttack(PlayerMoveCategory category, SkillAttackType attackType)
        {
            lastAttackCategory = category;
            if (category == PlayerMoveCategory.Sword)
                Play(oneShotSource, attackType == SkillAttackType.Heavy ? palette.heavySwordSwing : palette.swordSwing, combatVolume, palette,
                    attackType == SkillAttackType.Heavy ? palette.heavySwordSwingVolume : palette.swordSwingVolume);
            else if (category == PlayerMoveCategory.Punch || category == PlayerMoveCategory.Kick || category == PlayerMoveCategory.Basic)
                Play(oneShotSource, palette.unarmedSwing, combatVolume, palette, palette.unarmedSwingVolume);
        }

        void PlayGuardClash() => Play(oneShotSource, palette.guardClash, combatVolume, palette, palette.guardClashVolume);
        void PlayBowDraw() { lastAttackCategory = PlayerMoveCategory.Bow; Play(oneShotSource, palette.bowDraw, combatVolume, palette, palette.bowDrawVolume); }
        void PlayBowFire() { lastAttackCategory = PlayerMoveCategory.Bow; Play(oneShotSource, palette.bowFire, combatVolume, palette, palette.bowFireVolume); }
        void PlayMirrorBlade() { lastAttackCategory = PlayerMoveCategory.Skill; Play(oneShotSource, palette.mirrorBladeRelease, combatVolume, palette, palette.mirrorBladeReleaseVolume); }
        void PlayHurt() => Play(oneShotSource, palette.playerHurt, reactionVolume, palette, palette.playerHurtVolume);
        void PlayDeath() => Play(oneShotSource, palette.playerDeath, reactionVolume, palette, palette.playerDeathVolume);
        void PlayHeal() => Play(oneShotSource, palette.heal, reactionVolume, palette, palette.healVolume);

        void OnAttackConnected(GameObject target)
        {
            if (target && (target.GetComponentInParent<HealthResourceNode>() || target.GetComponentInParent<MirrorShatterEffect>()))
                return;
            var clip = lastAttackCategory == PlayerMoveCategory.Punch ||
                       lastAttackCategory == PlayerMoveCategory.Kick ||
                       lastAttackCategory == PlayerMoveCategory.Basic
                ? palette.unarmedHit
                : palette.swordHit;
            var audioVolume = lastAttackCategory == PlayerMoveCategory.Punch ||
                              lastAttackCategory == PlayerMoveCategory.Kick ||
                              lastAttackCategory == PlayerMoveCategory.Basic
                ? palette.unarmedHitVolume
                : palette.swordHitVolume;
            Play(oneShotSource, clip, combatVolume, palette, audioVolume);
        }

        void OnBowArrowConnected() => Play(oneShotSource, palette.arrowHit, combatVolume, palette, palette.arrowHitVolume);
        void OnMirrorBladeConnected() => Play(oneShotSource, palette.swordHit, combatVolume, palette, palette.swordHitVolume);

        public static void PlayEnemyAttack() => PlayShared(p => p.enemyAttack, p => p.enemyAttackVolume, 0.62f);
        public static void PlayEnemyHurt() => PlayShared(p => p.enemyHurt, p => p.enemyHurtVolume, 0.62f);
        public static void PlayEnemyDeath() => PlayShared(p => p.enemyDeath, p => p.enemyDeathVolume, 0.68f);
        public static void PlayBossCharge() => PlayShared(p => p.bossCharge, p => p.bossChargeVolume, 0.72f);
        public static void PlayBossSwordSwing(bool heavy) => PlayShared(p => heavy ? p.heavySwordSwing : p.swordSwing,
            p => heavy ? p.heavySwordSwingVolume : p.swordSwingVolume, 0.72f);
        public static void PlaySharedBowDraw() => PlayShared(p => p.bowDraw, p => p.bowDrawVolume, 0.7f);
        public static void PlaySharedBowFire() => PlayShared(p => p.bowFire, p => p.bowFireVolume, 0.72f);
        public static void PlaySharedArrowHit() => PlayShared(p => p.arrowHit, p => p.arrowHitVolume, 0.72f);
        public static void PlayTeleport() => PlayShared(p => p.teleport, p => p.teleportVolume, 0.7f);
        public static void PlaySummon() => PlayShared(p => p.summon, p => p.summonVolume, 0.72f);
        public static void PlayComplete() => PlayShared(p => p.uiComplete, p => p.uiCompleteVolume, 0.72f);

        public static bool TryPlayMirrorImpact(bool shatter)
        {
            if (!instance || !instance.palette || !instance.oneShotSource)
                return false;

            var clip = shatter ? instance.palette.mirrorShatter : instance.palette.mirrorHit;
            var volume = shatter ? instance.palette.mirrorShatterVolume : instance.palette.mirrorHitVolume;
            if (!clip)
                return false;

            Play(instance.oneShotSource, clip, 1f, instance.palette, volume);
            return true;
        }

        static void PlayShared(System.Func<GameAudioPalette, AudioClip> selectClip,
            System.Func<GameAudioPalette, float> selectVolume, float volume)
        {
            if (!instance || !instance.palette) return;
            Play(instance.oneShotSource, selectClip(instance.palette), volume, instance.palette, selectVolume(instance.palette));
        }
    }
}
