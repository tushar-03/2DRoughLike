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
            Torch(chunk, art, "torch", xa, F + 3.1f, 8f, biome.trim);
            Torch(chunk, art, "torch", xb, F + 3.1f, 8f, biome.trim);

            // braziers flank the rooms that matter
            if (!vertical && cell.Role != RoomRole.Combat)
            {
                Torch(chunk, art, "brazier", 10.5f, F, 9f, biome.trim);
                Torch(chunk, art, "brazier", 19.5f, F, 9f, biome.trim);
            }
        }

        static readonly System.Collections.Generic.Dictionary<Color, Sprite> Pillars = new System.Collections.Generic.Dictionary<Color, Sprite>();

        /// <summary>A stone post from the floor up to a torch, so the torch stands on something instead of hanging in the air.</summary>
        static Sprite PillarSprite(Color stone)
        {
            if (Pillars.TryGetValue(stone, out var cached) && cached != null) return cached;
            const int W = 16, H = 50;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color mid = stone, lit = Color.Lerp(stone, Color.white, 0.25f), dark = stone * 0.55f, line = stone * 0.4f;
            mid.a = lit.a = dark.a = line.a = 1f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool foot = y < 5, cap = y >= H - 4;
                    bool shaft = x >= 3 && x <= 12;
                    Color c = Color.clear;
                    if (foot || cap || shaft)
                    {
                        float half = (foot || cap) ? 8f : 5f;
                        float u = (x + 0.5f - 8f) / half;                 // -1 left edge .. +1 right edge
                        c = u < -0.45f ? lit : (u > 0.45f ? dark : mid);
                        if (!foot && !cap && y % 11 == 0) c = line;       // courses of stone
                        if ((foot && y == 4) || (cap && y == H - 4)) c = lit;
                        if (y == 0 || (cap && y == H - 1)) c = line;
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply(false, true);
            return Pillars[stone] = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 16f);
        }

        static void Torch(RoomChunk chunk, EnvironmentArt art, string clip, float x, float y, float radius, Color stone)
        {
            if (clip == "torch")
            {
                float F = ChunkMetrics.FloorTop;
                var post = new GameObject("Torch post");
                post.transform.SetParent(chunk.transform, false);
                post.transform.localPosition = new Vector3(x, F, 0f);
                var psr = post.AddComponent<SpriteRenderer>();
                psr.sprite = PillarSprite(stone);
                psr.sortingOrder = -9;
                post.transform.localScale = new Vector3(1f, (y - F + 0.05f) / 3.125f, 1f);
            }

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
