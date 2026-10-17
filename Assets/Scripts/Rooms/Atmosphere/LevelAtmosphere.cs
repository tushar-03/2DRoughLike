using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Roughlike.Utils;

namespace Roughlike.Rooms
{
    /// <summary>
    /// The mood of a level: ambient 2D light, a warm light carried by the player, the parallax backdrop and the post-processing
    /// stack (bloom, vignette, grading, grain). Everything comes from the biome's <see cref="EnvironmentArt"/>; with no art
    /// assigned nothing is created and the level stays graybox.
    /// </summary>
    public class LevelAtmosphere : MonoBehaviour
    {
        Light2D _ambient, _playerLight;
        Volume _volume;
        VolumeProfile _profile;

        public static LevelAtmosphere Create(EnvironmentArt art, Transform player)
        {
            var go = new GameObject("Atmosphere");
            var a = go.AddComponent<LevelAtmosphere>();
            a.Build(art, player);
            return a;
        }

        void Build(EnvironmentArt art, Transform player)
        {
            // ambient fill
            var amb = new GameObject("Ambient Light");
            amb.transform.SetParent(transform, false);
            _ambient = amb.AddComponent<Light2D>();
            _ambient.lightType = Light2D.LightType.Global;
            _ambient.color = art.ambient;
            _ambient.intensity = art.ambientIntensity;

            // the lantern the player carries
            if (player != null)
            {
                var pl = new GameObject("Player Light");
                pl.transform.SetParent(player, false);
                pl.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                _playerLight = pl.AddComponent<Light2D>();
                _playerLight.lightType = Light2D.LightType.Point;
                _playerLight.color = art.playerLight;
                _playerLight.intensity = 0.55f;
                _playerLight.pointLightOuterRadius = 8.5f;
                _playerLight.pointLightInnerRadius = 0.8f;
                _playerLight.falloffIntensity = 0.55f;
                pl.AddComponent<TorchFlicker>();
            }

            BuildPostProcessing(art);
            ParallaxBackdrop.Create(art).transform.SetParent(transform, false);
        }

        void BuildPostProcessing(EnvironmentArt art)
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Atmosphere Profile";

            var bloom = _profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(art.bloom);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.85f));

            var vig = _profile.Add<Vignette>(true);
            vig.intensity.Override(art.vignette);
            vig.smoothness.Override(0.55f);
            vig.color.Override(new Color(0.03f, 0.02f, 0.06f));

            var grade = _profile.Add<ColorAdjustments>(true);
            grade.colorFilter.Override(art.colourFilter);
            grade.saturation.Override(art.saturation);
            grade.contrast.Override(art.contrast);

            var grain = _profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(art.grain);
            grain.response.Override(0.8f);

            var go = new GameObject("Post Volume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10;
            _volume.sharedProfile = _profile;

            Camera cam = Camera.main;
            if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }
    }
}
