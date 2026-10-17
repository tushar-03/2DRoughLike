using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Enemies
{
    /// <summary>
    /// Chooses the enemy's animation clip from its AI state. The telegraph and attack clips are scrubbed by the state's own
    /// progress, so the anticipation pose always lands exactly when the danger area finishes filling.
    /// Missing clips fall back (walk to idle, attack to telegraph), so a partial art set still works.
    /// </summary>
    public class EnemyAnimDriver : MonoBehaviour
    {
        EnemyController _enemy;
        SpriteAnimator _anim;

        void Awake()
        {
            _anim = GetComponent<SpriteAnimator>();
            _enemy = GetComponentInParent<EnemyController>();
        }

        string Pick(string clip, string fallback) => _anim.Art.Has(clip) ? clip : fallback;

        void LateUpdate()
        {
            if (_enemy == null || _anim == null || _anim.Art == null || _enemy.Definition == null) return;
            EnemyDefinition d = _enemy.Definition;

            switch (_enemy.State)
            {
                case EnemyState.Patrol:
                case EnemyState.Chase:
                    _anim.Loop(_enemy.Speed > 0.4f ? Pick("walk", "idle") : "idle");
                    break;

                case EnemyState.Telegraph:
                    _anim.Scrub(Pick("telegraph", "idle"), Mathf.Clamp01(_enemy.StateTime / Mathf.Max(0.01f, _enemy.TelegraphTime)));
                    break;

                case EnemyState.Attack:
                    if (d.activeTime > 0.4f) _anim.Loop(Pick("attack", "idle"));            // charges and dives: keep galloping / diving
                    else _anim.Scrub(Pick("attack", Pick("telegraph", "idle")), Mathf.Clamp01(_enemy.StateTime / Mathf.Max(0.2f, d.activeTime)));
                    break;

                case EnemyState.Recover:
                    if (_enemy.StateTime < 0.25f) _anim.Scrub(Pick("attack", Pick("telegraph", "idle")), 1f);
                    else _anim.Loop("idle");
                    break;

                case EnemyState.Stagger:
                    _anim.Scrub(Pick("hurt", "idle"), Mathf.Clamp01(_enemy.StateTime / 0.2f));
                    break;

                case EnemyState.Dead:
                    _anim.Scrub(Pick("hurt", "idle"), 1f);
                    break;
            }
        }
    }
}
