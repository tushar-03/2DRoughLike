using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Player
{
    /// <summary>
    /// Builds a fully wired player GameObject from code. Used by the prefab builder (editor) and by tests,
    /// so there is exactly one definition of what a player is made of.
    /// </summary>
    public static class PlayerFactory
    {
        public enum InputKind { Device, Scripted }

        public static PlayerController Build(MovementConfig config, InputKind input, Sprite bodySprite = null,
            Vector2? position = null, string name = "Player")
        {
            var go = new GameObject(name);
            go.SetActive(false); // configure before Awake runs

            go.layer = GameLayers.Player;
            go.transform.position = position ?? Vector2.zero;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            rb.gravityScale = 0f;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = config.colliderSize;
            box.offset = new Vector2(0f, config.colliderSize.y * 0.5f);

            go.AddComponent<KinematicMotor2D>();

            if (input == InputKind.Device) go.AddComponent<PlayerInputReader>();
            else go.AddComponent<ScriptedIntentSource>();

            var controller = go.AddComponent<PlayerController>();
            controller.Configure(config);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = bodySprite;
            sr.sortingOrder = 10;
            visual.AddComponent<PlayerVisual>();
            // PlayerVisual finds the controller in its parent and uses its own transform as the body.

            go.SetActive(true);
            return controller;
        }
    }
}


