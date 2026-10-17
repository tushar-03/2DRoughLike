using UnityEngine;
using Roughlike.Combat;
using Roughlike.Utils;

namespace Roughlike.Enemies
{
    /// <summary>
    /// A slow guard with a big frontal shield. Hits from the front are mostly absorbed (they also build up shield damage);
    /// a heavy blow, enough accumulated hits or a parry breaks the shield for a few seconds. Hits from behind do extra damage.
    /// Its own attack is a shield bash, telegraphed like any other.
    /// </summary>
    public class ShieldBehavior : EnemyBehavior
    {
        float _brokenTimer;
        float _shieldPoise;
        SpriteRenderer _shieldSprite;

        public bool ShieldBroken => _brokenTimer > 0f;
        public bool ShieldUp => _brokenTimer <= 0f && E.State != EnemyState.Stagger && E.State != EnemyState.Recover && E.State != EnemyState.Dead;

        Sprite _intact, _broken;

        protected override void OnBind()
        {
            if (E.Visual == null) return;

            // painted art ships the shield as a separate prop, drawn in the body's own frame so it needs no placement
            if (D.art != null && D.art.Has("shield"))
            {
                _intact = D.art.Prop("shield");
                _broken = D.art.Has("shield_broken") ? D.art.Prop("shield_broken") : _intact;
                var prop = new GameObject("Shield");
                prop.transform.SetParent(E.Visual, false);
                _shieldSprite = prop.AddComponent<SpriteRenderer>();
                _shieldSprite.sprite = _intact;
                _shieldSprite.sortingOrder = 9;
                return;
            }

            var go = new GameObject("Shield");
            go.transform.SetParent(E.Visual, false);
            go.transform.localPosition = new Vector3(D.size.x * 0.5f + 0.1f, D.size.y * 0.5f, 0f);
            go.transform.localScale = new Vector3(0.28f, D.size.y * 0.95f, 1f);
            _shieldSprite = go.AddComponent<SpriteRenderer>();
            _shieldSprite.sprite = ProceduralSprites.Square;
            _shieldSprite.sortingOrder = 9;
            _shieldSprite.color = new Color(0.75f, 0.85f, 0.95f);
        }

        public override Vector2 Chase(float dt)
        {
            UpdateShieldVisual();

            int dir = E.DirToTarget;
            if (E.HorizontalDistance <= D.attackRange && E.TargetOnSameLevel && E.CanAttack) { E.BeginTelegraph(); return Vector2.zero; }
            if (E.HorizontalDistance <= D.attackRange * 0.7f) return Vector2.zero;
            return new Vector2(WalkToward(dir, E.Spd(D.chaseSpeed)), 0f);
        }

        public override TelegraphSpec Telegraph()
            => new TelegraphSpec { Centre = E.MeleeCentre(), Size = D.hitSize };

        public override Vector2 AttackTick(float dt)
        {
            E.StrikeBox(E.MeleeCentre(), D.hitSize, E.MakeAttack(DamageTags.Melee));
            return Vector2.zero;
        }

        public override void Tick(float dt)
        {
            if (_brokenTimer > 0f) _brokenTimer = Mathf.Max(0f, _brokenTimer - dt);
            UpdateShieldVisual();
        }

        void UpdateShieldVisual()
        {
            if (_shieldSprite == null) return;
            if (_intact != null)
            {
                _shieldSprite.sprite = ShieldBroken ? _broken : _intact;
                return;
            }
            _shieldSprite.color = ShieldUp ? new Color(0.75f, 0.85f, 0.95f) : new Color(0.4f, 0.4f, 0.45f, 0.5f);
        }

        bool FromFront(in DamageInfo info)
        {
            float dx = info.SourcePosition.x - E.Position.x;
            return Mathf.Abs(dx) < 0.1f || (int)Mathf.Sign(dx) == E.Facing;
        }

        public override HitResult Filter(ref DamageInfo info)
        {
            if ((info.Tags & DamageTags.Dot) != 0) return HitResult.Hit;

            if (!FromFront(info))
            {
                info.Amount *= D.backstabMultiplier;     // nothing protects the back
                return HitResult.Hit;
            }
            if (!ShieldUp || (info.Tags & DamageTags.Unblockable) != 0) return HitResult.Hit;

            _shieldPoise += info.Stagger;
            if (info.Stagger >= D.shieldBreakStagger || _shieldPoise >= D.shieldBreakStagger * 1.5f)
            {
                BreakShield();
                info.Amount *= 0.5f;                     // the breaking blow still hurts a little less
                return HitResult.Hit;
            }

            info.Amount *= 1f - D.shieldReduction;
            info.Knockback = Vector2.zero;
            info.Stagger = 0f;
            return HitResult.Blocked;
        }

        void BreakShield()
        {
            _brokenTimer = D.shieldBreakTime;
            _shieldPoise = 0f;
        }

        public override void OnStaggered()
        {
            // a parry (long stun) shatters the shield; a normal flinch does not
            if (E.StaggerDuration > D.staggerTime + 0.05f) BreakShield();
        }
    }
}
