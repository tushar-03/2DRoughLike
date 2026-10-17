using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Roughlike.Rooms
{
    /// <summary>Gives a 2D light a lively, uneven flicker like a real flame.</summary>
    public class TorchFlicker : MonoBehaviour
    {
        Light2D _light;
        float _base, _seed;

        void Awake()
        {
            _light = GetComponent<Light2D>();
            _seed = Random.value * 100f;
        }

        void Start()
        {
            if (_light != null) _base = _light.intensity;
        }

        void Update()
        {
            if (_light == null) return;
            float t = Time.time * 7f + _seed;
            float n = Mathf.PerlinNoise(t, _seed) * 0.6f + Mathf.PerlinNoise(t * 2.7f, _seed + 5f) * 0.4f;
            _light.intensity = _base * (0.82f + n * 0.36f);
        }
    }
}
