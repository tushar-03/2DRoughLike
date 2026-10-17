using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Enemies
{
    /// <summary>Builds a fully wired boss from its definition. Shared by the level builder and tests.</summary>
    public static class BossFactory
    {
        public static BossBrain BrainFor(BossKind kind)
        {
            switch (kind)
            {
                case BossKind.Warden: return new WardenBrain();
                default: return new WitchBrain();
            }
        }

        public static BossController Build(BossDefinition def, Vector2 feet, EnemyScaling scaling, Rect arena)
        {
            var go = new GameObject(def.displayName);
            go.SetActive(false);   // wire everything before Awake runs
            go.layer = GameLayers.Enemy;
            go.transform.position = feet;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = def.size;
            box.offset = new Vector2(0f, def.size.y * 0.5f);

            var motor = go.AddComponent<KinematicMotor2D>();
            motor.ConfigureBody(def.size);

            var health = go.AddComponent<Health>();
            health.Initialize(Team.Enemy, def.maxHealth * scaling.Health);
            go.AddComponent<StatusEffects>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(visual, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;
            SpriteAnimator anim = null;
            if (def.art != null)
            {
                sr.sprite = def.art.Prop("idle");
                sr.color = Color.white;
                anim = spriteGo.AddComponent<SpriteAnimator>();
                anim.Bind(def.art);
                anim.Loop("idle");
            }
            else
            {
                sr.sprite = ProceduralSprites.Square;
                sr.color = def.tint;
                spriteGo.transform.localScale = new Vector3(def.size.x, def.size.y, 1f);
                spriteGo.transform.localPosition = new Vector3(0f, def.size.y * 0.5f, 0f);
            }
            var react = spriteGo.AddComponent<HitReactionVisual>();
            react.Bind(sr, health, false);

            var boss = go.AddComponent<BossController>();
            boss.Arena = arena;
            boss.Initialize(def, scaling, BrainFor(def.kind), sr, anim, react, visual);

            go.SetActive(true);
            return boss;
        }
    }
}
