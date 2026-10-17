using UnityEngine;
using Roughlike.Core;
using Roughlike.Player;
using Roughlike.Utils;

namespace Roughlike.Combat
{
    /// <summary>
    /// Turns combat events into game feel: hitstop, screen shake, sparks, damage numbers and slash trails.
    /// Place one in a scene. Combat code only raises events; it never calls into this class.
    /// All intensities are accessibility-adjustable (see the Inspector / settings in M10).
    /// </summary>
    public class CombatFeedbackHub : MonoBehaviour
    {
        [Header("Accessibility / settings")]
        public bool showDamageNumbers = true;
        [Range(0f, 1f)] public float hitStopScale = 1f;
        [Range(0f, 1f)] public float screenShakeScale = 1f;
        [Range(0f, 1f)] public float flashScale = 1f;

        [Header("Colours")]
        [SerializeField] Color enemyHitColour = new Color(1f, 0.95f, 0.8f);
        [SerializeField] Color playerHurtColour = new Color(1f, 0.3f, 0.25f);
        [SerializeField] Color parryColour = new Color(1f, 0.85f, 0.3f);
        [SerializeField] Color blockColour = new Color(0.7f, 0.85f, 1f);

        ComponentPool<SparkVfx> _sparks;
        ComponentPool<SlashVfx> _slashes;
        ComponentPool<DamageNumber> _numbers;

        void Awake()
        {
            _sparks = new ComponentPool<SparkVfx>(() =>
            {
                var go = new GameObject("Spark"); go.transform.SetParent(transform, false);
                var s = go.AddComponent<SparkVfx>(); s.Init(_sparks.Release); return s;
            });
            _slashes = new ComponentPool<SlashVfx>(() =>
            {
                var go = new GameObject("Slash"); go.transform.SetParent(transform, false);
                var s = go.AddComponent<SlashVfx>(); s.Init(_slashes.Release); return s;
            });
            _numbers = new ComponentPool<DamageNumber>(() =>
            {
                var go = new GameObject("DamageNumber"); go.transform.SetParent(transform, false);
                var s = go.AddComponent<DamageNumber>(); s.Init(_numbers.Release); return s;
            });
        }

        void OnEnable()
        {
            EventBus<HitEvent>.Subscribe(OnHit);
            EventBus<SwingEvent>.Subscribe(OnSwing);
            EventBus<ExplosionEvent>.Subscribe(OnExplosion);
            ApplySettings();
        }

        void OnDisable()
        {
            EventBus<HitEvent>.Unsubscribe(OnHit);
            EventBus<SwingEvent>.Unsubscribe(OnSwing);
            EventBus<ExplosionEvent>.Unsubscribe(OnExplosion);
            HitStop.Cancel();
        }

        void OnExplosion(ExplosionEvent e)
        {
            Haptics.Play(HapticKind.Explosion);
            Spark(e.Position, e.Radius * 1.8f, e.Colour);
            Spark(e.Position + new Vector2(0.3f, 0.2f), e.Radius, Color.white);
            ScreenShake.Shake(0.45f, Vector2.up);
            HitStop.Request(0.05f);
        }

        void OnValidate() => ApplySettings();

        void ApplySettings()
        {
            HitStop.Scale = hitStopScale;
            ScreenShake.Scale = screenShakeScale;
            HitReactionVisual.FlashScale = flashScale;
        }

        void OnSwing(SwingEvent e)
        {
            var s = _slashes.Get();
            s.Play(e.Position, e.Facing, e.Size, e.Tint, 0.14f);
        }

        void OnHit(HitEvent evt)
        {
            HitReport r = evt.Report;
            if (r.Result == HitResult.Ignored) return;

            bool playerHurt = r.Target.Team == Team.Player;

            // damage over time: a small number only, no spark, hitstop or shake
            if ((r.Info.Tags & DamageTags.Dot) != 0)
            {
                Number(r.Position + Vector2.up * 0.5f, Mathf.Max(1, Mathf.RoundToInt(r.Amount)).ToString(),
                    new Color(1f, 0.6f, 0.25f), 0.14f, 0.5f);
                return;
            }
            Vector2 dir = r.Info.Knockback.sqrMagnitude > 0.01f ? r.Info.Knockback.normalized : Vector2.right;

            PlayHaptics(r, playerHurt);

            switch (r.Result)
            {
                case HitResult.Hit:
                    Spark(r.Position, Mathf.Lerp(0.9f, 1.8f, Mathf.Clamp01(r.Amount / 25f)), playerHurt ? playerHurtColour : enemyHitColour);
                    Number(r.Position + Vector2.up * 0.6f, Mathf.RoundToInt(r.Amount).ToString(),
                        playerHurt ? playerHurtColour : (r.Info.IsCrit ? parryColour : Color.white), r.Info.IsCrit ? 0.3f : 0.2f);
                    break;
                case HitResult.Blocked:
                    Spark(r.Position, 1.0f, blockColour);
                    Number(r.Position + Vector2.up * 0.6f, r.Amount > 0.5f ? Mathf.RoundToInt(r.Amount).ToString() : "BLOCK", blockColour, 0.17f);
                    break;
                case HitResult.Parried:
                    Spark(r.Position, 2.2f, parryColour);
                    Number(r.Position + Vector2.up * 0.9f, "PARRY!", parryColour, 0.28f, 0.9f);
                    break;
            }

            HitStop.Request(r.Info.HitStop);
            ScreenShake.Shake(r.Info.Shake * (playerHurt ? 1.4f : 1f), dir);
        }

        /// <summary>Rumble: hard when you are hurt, light when you land a hit, a sharp snap on a parry.</summary>
        static void PlayHaptics(HitReport r, bool playerHurt)
        {
            switch (r.Result)
            {
                case HitResult.Parried: Haptics.Play(HapticKind.Parry); break;
                case HitResult.Blocked: if (playerHurt) Haptics.Play(HapticKind.Medium); else Haptics.Play(HapticKind.Light); break;
                case HitResult.Hit:
                    if (playerHurt) Haptics.Play(r.Killed ? HapticKind.Death : (r.Amount >= 18f ? HapticKind.Heavy : HapticKind.Medium));
                    else if (r.Info.SourceTeam == Team.Player) Haptics.Play(r.Info.IsCrit || r.Killed ? HapticKind.Medium : HapticKind.Light);
                    break;
            }
        }

        void Spark(Vector2 pos, float size, Color c) => _sparks.Get().Play(pos, size, c);

        /// <summary>Global switch from the settings menu; combined with the per-hub toggle.</summary>
        public static bool DamageNumbersEnabled = true;

        void Number(Vector2 pos, string text, Color c, float size, float life = 0.7f)
        {
            if (!showDamageNumbers || !DamageNumbersEnabled) return;
            _numbers.Get().Play(pos, text, c, size, life);
        }
    }
}
