using System;
using UnityEngine;
using Roughlike.Core;
using Roughlike.Player;
using Roughlike.Utils;

namespace Roughlike.Rooms
{
    /// <summary>The level exit. Stand in the doorway and press Up to leave (Cells are banked here from M8).</summary>
    public class ExitDoor : MonoBehaviour
    {
        public event Action Activated;

        [SerializeField] Vector2 size = new Vector2(2.4f, 3.2f);
        TextMesh _prompt;
        bool _used;

        public bool Used => _used;

        public static ExitDoor Create(Transform parent, Vector2 feetPosition, Color accent)
        {
            var go = new GameObject("Exit Door");
            go.transform.SetParent(parent, false);
            go.transform.position = feetPosition;

            var frame = new GameObject("Frame");
            frame.transform.SetParent(go.transform, false);
            frame.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            frame.transform.localScale = new Vector3(2.2f, 3f, 1f);
            var fsr = frame.AddComponent<SpriteRenderer>();
            fsr.sprite = ProceduralSprites.Square; fsr.sortingOrder = -2;
            fsr.color = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 1f);

            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            glow.transform.localScale = new Vector3(1.6f, 2.6f, 1f);
            var gsr = glow.AddComponent<SpriteRenderer>();
            gsr.sprite = ProceduralSprites.Square; gsr.sortingOrder = -1;
            gsr.color = new Color(accent.r, accent.g, accent.b, 0.55f);

            var label = new GameObject("Prompt");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 3.9f, 0f);
            var tm = label.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font; tm.fontSize = 48; tm.characterSize = 0.1f; tm.anchor = TextAnchor.MiddleCenter;
            tm.text = "EXIT"; tm.color = new Color(1f, 1f, 1f, 0.9f);
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.GetComponent<MeshRenderer>().sortingOrder = 6;

            var door = go.AddComponent<ExitDoor>();
            door._prompt = tm;
            return door;
        }

        void Update()
        {
            if (_used) return;
            Vector2 centre = (Vector2)transform.position + new Vector2(0f, size.y * 0.5f);
            var col = Physics2D.OverlapBox(centre, size, 0f, LayerMask.GetMask(GameLayers.PlayerName));
            PlayerController player = col != null ? col.GetComponentInParent<PlayerController>() : null;

            if (_prompt != null) _prompt.text = player != null ? "Press UP to descend" : "EXIT";
            if (player != null && player.LastIntent.UpHeld())
            {
                _used = true;
                Activated?.Invoke();
            }
        }

        /// <summary>For tests and debug tools.</summary>
        public void Trigger()
        {
            if (_used) return;
            _used = true;
            Activated?.Invoke();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube((Vector2)transform.position + new Vector2(0f, size.y * 0.5f), size);
        }
    }
}
