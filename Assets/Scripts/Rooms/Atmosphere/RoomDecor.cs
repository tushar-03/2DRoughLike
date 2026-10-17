using UnityEngine;
using UnityEngine.Rendering.Universal;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Rooms
{
    /// <summary>Dresses a built room: wall torches and braziers with flickering warm lights. Deterministic per room.</summary>
    public static class RoomDecor
    {
        public static void Populate(RoomChunk chunk, LevelCell cell, BiomeData biome)
        {
            EnvironmentArt art = biome.art;
            if (art == null || art.props == null) return;

            bool vertical = (cell.Mask & (Openings.Up | Openings.Down)) != 0;
            float F = ChunkMetrics.FloorTop;
            var rng = new SeededRng((uint)(cell.Index * 7919 + 13));

            // two wall torches on the back wall; moved to the edges in shaft rooms where the stair fills the middle
            float xa = vertical ? 3.4f : 7f + rng.Range(-1f, 1f);
            float xb = vertical ? 26.6f : 23f + rng.Range(-1f, 1f);
            Torch(chunk, art, "torch", xa, F + 3.1f, 8f);
            Torch(chunk, art, "torch", xb, F + 3.1f, 8f);

            // braziers flank the rooms that matter
            if (!vertical && cell.Role != RoomRole.Combat)
            {
                Torch(chunk, art, "brazier", 10.5f, F, 9f);
                Torch(chunk, art, "brazier", 19.5f, F, 9f);
            }
        }

        static void Torch(RoomChunk chunk, EnvironmentArt art, string clip, float x, float y, float radius)
        {
            var go = new GameObject(clip);
            go.transform.SetParent(chunk.transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -8;
            var anim = go.AddComponent<SpriteAnimator>();
            anim.Bind(art.props);
            anim.Loop(clip);
            sr.sprite = art.props.Prop(clip);
            go.transform.localScale = Vector3.one * (clip == "brazier" ? 1.15f : 1f);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, clip == "brazier" ? 0.9f : 0.75f, 0f);
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = art.torchLight;
            light.intensity = clip == "brazier" ? 1.25f : 1.05f;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = 0.6f;
            light.falloffIntensity = 0.5f;
            lightGo.AddComponent<TorchFlicker>();
        }
    }
}
