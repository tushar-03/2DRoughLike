using Unity.Cinemachine;
using UnityEngine;

namespace Roughlike.Combat
{
    /// <summary>
    /// Screen shake through Cinemachine impulses. The camera needs a CinemachineImpulseListener.
    /// Accessibility: <see cref="Scale"/> (0 to 1) is the player's shake slider.
    /// </summary>
    public static class ScreenShake
    {
        public static float Scale = 1f;

        static CinemachineImpulseSource _source;

        public static void Shake(float strength, Vector2 direction)
        {
            strength *= Scale;
            if (strength <= 0.001f) return;

            if (_source == null)
            {
                var go = new GameObject("[ScreenShake]") { hideFlags = HideFlags.HideAndDontSave };
                if (Application.isPlaying) Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<CinemachineImpulseSource>();
                _source.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
                _source.ImpulseDefinition.ImpulseDuration = 0.18f;
            }

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            _source.GenerateImpulseWithVelocity(new Vector3(dir.x, dir.y, 0f) * strength);
        }
    }
}
