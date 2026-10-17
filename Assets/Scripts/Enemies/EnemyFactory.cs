using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Enemies
{
    /// <summary>Builds a fully wired enemy from a definition. One definition of "what an enemy is made of", shared by tests and the scene builder.</summary>
    public static class EnemyFactory
    {
        public static EnemyController Build(EnemyDefinition def, Vector2 position, bool withHealthBar = true)
        {
            var go = new GameObject(def.displayName);
            go.SetActive(false);   // wire everything before Awake runs
            go.layer = GameLayers.Enemy;
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = def.size;
            box.offset = new Vector2(0f, def.size.y * 0.5f);

            var motor = go.AddComponent<KinematicMotor2D>();
            motor.ConfigureBody(def.size);

            var health = go.AddComponent<Health>();
            health.Initialize(Team.Enemy, def.maxHealth);
            go.AddComponent<StatusEffects>();

            // visuals: root (flipped by facing) -> sprite (squash, flash)
            var visualRoot = new GameObject("Visual").transform;
            visualRoot.SetParent(go.transform, false);
            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(visualRoot, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 8;
            SpriteAnimator animator = null;
            if (def.art != null)
            {
                // painted art carries its own colours and is already feet-pivoted at world scale
                sr.sprite = def.art.Prop("idle");
                sr.color = Color.white;
                animator = spriteGo.AddComponent<SpriteAnimator>();
                animator.Bind(def.art);
                animator.Loop("idle");
                spriteGo.AddComponent<EnemyAnimDriver>();
            }
            else if (def.sprite != null)
            {
                sr.sprite = def.sprite;
                sr.color = def.tint;
            }
            else
            {
                sr.sprite = ProceduralSprites.Square;
                sr.color = def.tint;
                spriteGo.transform.localScale = new Vector3(def.size.x, def.size.y, 1f);
                spriteGo.transform.localPosition = new Vector3(0f, def.size.y * 0.5f, 0f);
            }
            var react = spriteGo.AddComponent<HitReactionVisual>();
            react.Bind(sr, health, true);

            var telegraphGo = new GameObject("Telegraph");
            telegraphGo.transform.SetParent(go.transform, false);
            var telegraph = telegraphGo.AddComponent<TelegraphIndicator>();

            if (withHealthBar)
            {
                var barGo = new GameObject("HealthBar");
                barGo.transform.SetParent(go.transform, false);
                var bar = barGo.AddComponent<WorldHealthBar>();
                bar.Bind(health, def.size.y + 0.35f, Mathf.Max(0.9f, def.size.x + 0.2f));
            }

            var controller = go.AddComponent<EnemyController>();
            controller.Initialize(def, telegraph, react, visualRoot);

            go.SetActive(true);
            return controller;
        }

        /// <summary>A stationary punching bag.</summary>
        public static TrainingDummy BuildDummy(Vector2 position, Sprite sprite = null, Color? tint = null)
        {
            var go = new GameObject("Training Dummy");
            go.SetActive(false);
            go.layer = GameLayers.Enemy;
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var box = go.AddComponent<BoxCollider2D>();
            Vector2 size = new Vector2(0.9f, 1.7f);
            box.size = size;
            box.offset = new Vector2(0f, size.y * 0.5f);

            var health = go.AddComponent<Health>();
            health.Initialize(Team.Enemy, 100000f);
            go.AddComponent<StatusEffects>();

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(go.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 8;
            Color c = tint ?? new Color(0.85f, 0.7f, 0.4f);
            if (sprite != null) { sr.sprite = sprite; sr.color = c; }
            else
            {
                sr.sprite = ProceduralSprites.Square;
                sr.color = c;
                spriteGo.transform.localScale = new Vector3(size.x, size.y, 1f);
                spriteGo.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            }
            spriteGo.AddComponent<HitReactionVisual>().Bind(sr, health, true);

            var dummy = go.AddComponent<TrainingDummy>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, size.y + 0.7f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.1f;
            tm.fontSize = 48;
            tm.color = new Color(1f, 1f, 1f, 0.85f);
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            labelGo.GetComponent<MeshRenderer>().sortingOrder = 6;
            dummy.AttachLabel(tm);

            go.SetActive(true);
            return dummy;
        }
    }
}
