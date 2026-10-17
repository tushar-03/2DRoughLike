using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Player
{
    /// <summary>
    /// Grants traversal rune abilities when the player touches it. In the full game runes are bought with
    /// meta-progression (M8); here it lets the graybox test levels be completed without debug keys.
    /// </summary>
    public class RuneShrine : MonoBehaviour
    {
        [SerializeField] MovementAbilities grants = MovementAbilities.WallJump;
        [SerializeField] Vector2 size = new Vector2(1.6f, 3f);
        [SerializeField] SpriteRenderer glow;
        [SerializeField] TextMesh label;

        bool _taken;
        float _t;

        public void Configure(MovementAbilities abilities, Vector2 triggerSize, SpriteRenderer glowSprite, TextMesh text)
        {
            grants = abilities;
            size = triggerSize;
            glow = glowSprite;
            label = text;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (glow != null)
            {
                float pulse = 0.55f + 0.25f * Mathf.Sin(_t * (_taken ? 1.5f : 4f));
                Color c = _taken ? new Color(0.5f, 0.5f, 0.55f) : new Color(0.4f, 0.95f, 1f);
                c.a = pulse;
                glow.color = c;
            }
            if (_taken) return;

            var col = Physics2D.OverlapBox(transform.position, size, 0f, LayerMask.GetMask(GameLayers.PlayerName));
            if (col == null) return;
            var player = col.GetComponentInParent<PlayerController>();
            if (player == null) return;

            player.Abilities |= grants;
            _taken = true;
            if (label != null) label.text = grants + " rune acquired!";
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
