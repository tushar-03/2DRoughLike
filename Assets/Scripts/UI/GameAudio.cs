using UnityEngine;
using UnityEngine.EventSystems;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;
using Roughlike.Utils;

namespace Roughlike.UI
{
    /// <summary>
    /// Connects what happens in the game to sound: it listens to the player's events and the global event bus and plays the
    /// matching <see cref="SfxId"/>. Gameplay code stays free of audio logic (a few leaf spots call <see cref="Sfx"/> directly).
    /// One instance per scene, created with the UI.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        PlayerController _player;
        PlayerCombat _combat;
        Health _health;
        SkillRunner _skills;
        float _stepTimer;
        RunManager _run;
        BiomeData _lastBiome;

        public static GameAudio Create(Transform parent, PlayerController player, RunManager run = null)
        {
            var go = new GameObject("Game Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<GameAudio>();
            a._run = run;
            a.Bind(player);
            return a;
        }

        void Bind(PlayerController player)
        {
            _player = player;
            if (player == null) return;
            _combat = player.GetComponent<PlayerCombat>();
            _health = player.GetComponent<Health>();
            _skills = player.GetComponent<SkillRunner>();
            Hook(true);
        }

        void Hook(bool on)
        {
            if (_player != null)
            {
                if (on)
                {
                    _player.Jumped += OnJump; _player.DoubleJumped += OnDoubleJump; _player.WallJumped += OnWallJump;
                    _player.Landed += OnLand; _player.RollStarted += OnRoll; _player.LedgeGrabbed += OnLedge;
                }
                else
                {
                    _player.Jumped -= OnJump; _player.DoubleJumped -= OnDoubleJump; _player.WallJumped -= OnWallJump;
                    _player.Landed -= OnLand; _player.RollStarted -= OnRoll; _player.LedgeGrabbed -= OnLedge;
                }
            }
            if (_combat != null)
            {
                if (on) { _combat.Parried += OnParried; _combat.ShieldRaised += OnShield; }
                else { _combat.Parried -= OnParried; _combat.ShieldRaised -= OnShield; }
            }
            if (_health != null)
            {
                if (on) _health.Died += OnPlayerDied; else _health.Died -= OnPlayerDied;
            }
            if (_skills != null)
            {
                if (on) _skills.SkillUsed += OnSkill; else _skills.SkillUsed -= OnSkill;
            }
        }

        void OnEnable()
        {
            if (_player != null) Hook(true);
            EventBus<HitEvent>.Subscribe(OnHit);
            EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
            EventBus<ExplosionEvent>.Subscribe(OnExplosion);
            EventBus<BossSpawnedEvent>.Subscribe(OnBossSpawned);
            EventBus<BossDefeatedEvent>.Subscribe(OnBossDefeated);
            EventBus<ToastEvent>.Subscribe(OnToast);
            EventBus<ItemAcquiredEvent>.Subscribe(OnItem);
            EventBus<RoomClearedEvent>.Subscribe(OnRoomCleared);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            EventBus<OpenShopEvent>.Subscribe(OnOpenShop);
            EventBus<OpenMutationEvent>.Subscribe(OnOpenMutation);
        }

        void OnDisable()
        {
            if (_player != null) Hook(false);
            EventBus<HitEvent>.Unsubscribe(OnHit);
            EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
            EventBus<ExplosionEvent>.Unsubscribe(OnExplosion);
            EventBus<BossSpawnedEvent>.Unsubscribe(OnBossSpawned);
            EventBus<BossDefeatedEvent>.Unsubscribe(OnBossDefeated);
            EventBus<ToastEvent>.Unsubscribe(OnToast);
            EventBus<ItemAcquiredEvent>.Unsubscribe(OnItem);
            EventBus<RoomClearedEvent>.Unsubscribe(OnRoomCleared);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            EventBus<OpenShopEvent>.Unsubscribe(OnOpenShop);
            EventBus<OpenMutationEvent>.Unsubscribe(OnOpenMutation);
        }

        void OnDestroy() => Sfx.StopAllLoops();

        // ------------------------------------------------------------------ player

        void OnJump() => Sfx.Play(SfxId.Jump, 0.7f);
        void OnDoubleJump() => Sfx.Play(SfxId.DoubleJump, 0.7f);
        void OnWallJump() => Sfx.Play(SfxId.WallJump, 0.7f);
        void OnLand(float impact) { if (impact > 7f) Sfx.Play(SfxId.Land, Mathf.Clamp01(impact / 22f) * 0.8f + 0.2f); }
        void OnRoll() => Sfx.Play(SfxId.Roll, 0.8f);
        void OnLedge() => Sfx.Play(SfxId.LedgeGrab, 0.6f);
        void OnParried() => Sfx.Play(SfxId.Parry);
        void OnShield() => Sfx.Play(SfxId.ShieldRaise, 0.7f);
        void OnPlayerDied() => Sfx.Play(SfxId.PlayerDeath);
        void OnSkill(int slot, SkillData skill) => Sfx.Play(SfxId.Skill, 0.9f);

        void Update()
        {
            if (_run != null && _run.Biome != null && _run.Biome != _lastBiome)
            {
                _lastBiome = _run.Biome;
                StartAmbience(_run.Biome.windyAmbience);
            }

            // footsteps while running on the ground
            if (_player != null && _player.State == PlayerState.Grounded && Mathf.Abs(_player.Velocity.x) > 1.5f)
            {
                _stepTimer -= Time.deltaTime;
                if (_stepTimer <= 0f) { Sfx.Play(SfxId.Footstep, 0.35f, 1f, 0.12f); _stepTimer = 0.27f; }
            }
            else _stepTimer = Mathf.Min(_stepTimer, 0.08f);
        }

        // ------------------------------------------------------------------ bus events

        void OnHit(HitEvent e)
        {
            HitReport r = e.Report;
            if (r.Target == null) return;
            bool onPlayer = r.Target == _health;
            switch (r.Result)
            {
                case HitResult.Hit:
                    if (r.Info.Tags.HasFlag(DamageTags.Dot)) { Sfx.PlayAt(SfxId.Burn, r.Position, 0.35f); return; }
                    if (onPlayer) Sfx.Play(SfxId.PlayerHurt, 0.9f);
                    else if (r.Info.IsCrit) Sfx.PlayAt(SfxId.Crit, r.Position);
                    else Sfx.PlayAt(r.Amount >= 28f ? SfxId.HitHeavy : SfxId.Hit, r.Position, 0.85f);
                    break;
                case HitResult.Blocked:
                    Sfx.PlayAt(SfxId.Block, r.Position);
                    break;
            }
        }

        void OnEnemyDied(EnemyDiedEvent e) => Sfx.PlayAt(SfxId.EnemyDeath, e.Position, 0.9f);
        void OnExplosion(ExplosionEvent e) { if (!e.Cosmetic) PlayExplosion(e); }

        void PlayExplosion(ExplosionEvent e) => Sfx.PlayAt(SfxId.Explosion, e.Position, Mathf.Clamp01(e.Radius / 3f) * 0.6f + 0.4f);

        void OnBossSpawned(BossSpawnedEvent e)
        {
            Sfx.Play(SfxId.BossRoar);
            if (e.Boss != null) e.Boss.PhaseChanged += OnBossPhase;
        }

        void OnBossPhase(int phase) => Sfx.Play(SfxId.BossPhase);
        void OnBossDefeated(BossDefeatedEvent e) => Sfx.Play(SfxId.BossDeath);

        void OnToast(ToastEvent e) => Sfx.Play(SfxId.Toast, 0.5f);
        void OnItem(ItemAcquiredEvent e) => Sfx.Play(SfxId.ItemPickup);
        void OnRoomCleared(RoomClearedEvent e) => Sfx.Play(SfxId.RoomClear, 0.8f);
        void OnOpenShop(OpenShopEvent e) => Sfx.Play(SfxId.UiOpen);
        void OnOpenMutation(OpenMutationEvent e) => Sfx.Play(SfxId.Mutation);

        void OnRunStarted(RunStartedEvent e)
        {
            Sfx.Play(SfxId.LevelStart, 0.8f);
            _lastBiome = null;   // Update restarts the ambience for the biome now in play
        }

        /// <summary>Ambient drone and the crackle of torches under everything else.</summary>
        public void StartAmbience(bool promenade = false)
        {
            Sfx.StopLoop(SfxId.AmbientCatacombs);
            Sfx.StopLoop(SfxId.AmbientPromenade);
            Sfx.StartLoop(promenade ? SfxId.AmbientPromenade : SfxId.AmbientCatacombs);
            Sfx.StartLoop(SfxId.FireLoop);
        }
    }
}
