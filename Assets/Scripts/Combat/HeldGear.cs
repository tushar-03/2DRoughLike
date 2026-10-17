using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Combat
{
    /// <summary>
    /// Draws the weapon and shield the player is actually carrying on top of the body sprite. The body sheet stores, per frame,
    /// where the hand is and which way the blade points (<see cref="CharacterArt.Anchor"/>), so any weapon sprite follows every
    /// pose (idle, run, swing, block) without being baked into the animation. A shield rides on the back until it is raised.
    /// Lives on the body object, so facing flips and squash apply to the gear too.
    /// </summary>
    public class HeldGear : MonoBehaviour
    {
        SpriteAnimator _anim;
        SpriteRenderer _body;
        PlayerCombat _combat;
        SpriteRenderer _weapon, _shield;

        void Awake()
        {
            _anim = GetComponent<SpriteAnimator>();
            _body = GetComponent<SpriteRenderer>();
            _combat = GetComponentInParent<PlayerCombat>();
            _weapon = MakeRenderer("Held Weapon");
            _shield = MakeRenderer("Held Shield");
        }

        SpriteRenderer MakeRenderer(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = false;
            return sr;
        }

        /// <summary>The weapon sprite currently shown (null when hidden); a test seam.</summary>
        public Sprite ShownWeapon => _weapon != null && _weapon.enabled ? _weapon.sprite : null;
        public Sprite ShownShield => _shield != null && _shield.enabled ? _shield.sprite : null;
        public bool ShieldRaised { get; private set; }

        void LateUpdate()
        {
            if (_anim == null || _combat == null || _anim.Art == null) { Hide(); return; }
            if (!_anim.Art.TryAnchor(_anim.Clip, _anim.FrameIndex, out CharacterArt.Anchor a)) { Hide(); return; }

            int order = _body != null ? _body.sortingOrder : 0;
            Material mat = _body != null ? _body.sharedMaterial : null;

            WeaponData melee = _combat.HeldMelee;
            if (melee != null && melee.sprite != null)
            {
                _weapon.enabled = true;
                _weapon.sprite = melee.sprite;
                if (mat != null) _weapon.sharedMaterial = mat;
                _weapon.sortingOrder = order + 1;
                Transform t = _weapon.transform;
                t.localPosition = _anim.Art.PixelToLocal(a.hand);
                t.localRotation = Quaternion.Euler(0f, 0f, a.angle - 90f);
                t.localScale = Vector3.one * melee.heldScale;
            }
            else _weapon.enabled = false;

            WeaponData shield = _combat.HeldShield;
            ShieldRaised = a.shieldUp;
            if (shield != null && shield.sprite != null)
            {
                _shield.enabled = true;
                _shield.sprite = shield.sprite;
                if (mat != null) _shield.sharedMaterial = mat;
                Transform t = _shield.transform;
                if (a.shieldUp)
                {
                    _shield.sortingOrder = order + 2;
                    t.localPosition = _anim.Art.PixelToLocal(a.shield);
                    t.localRotation = Quaternion.identity;
                    t.localScale = Vector3.one * shield.heldScale;
                }
                else
                {
                    // slung on the back, peeking out behind the body
                    _shield.sortingOrder = order - 1;
                    t.localPosition = _anim.Art.PixelToLocal(a.stow);
                    t.localRotation = Quaternion.Euler(0f, 0f, 14f);
                    t.localScale = Vector3.one * shield.heldScale * 0.7f;
                }
            }
            else _shield.enabled = false;
        }

        void Hide()
        {
            if (_weapon != null) _weapon.enabled = false;
            if (_shield != null) _shield.enabled = false;
            ShieldRaised = false;
        }
    }
}
