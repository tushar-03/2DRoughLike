using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Roughlike.Core;
using Roughlike.Rooms;
using Roughlike.Utils;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>Painted levels must play exactly like graybox ones: art changes what you see, never the solid geometry.</summary>
    public class EnvironmentArtTests
    {
        readonly List<Object> _owned = new List<Object>();
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            WorldArt.Props = null;
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        Sprite TileSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            _owned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
            _owned.Add(s);
            return s;
        }

        CharacterArt PropsArt()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            _owned.Add(tex);
            var art = ScriptableObject.CreateInstance<CharacterArt>();
            _owned.Add(art);
            art.sheet = tex;
            art.frameWidth = 4; art.frameHeight = 4; art.pixelsPerUnit = 4f;
            art.clips = new[]
            {
                new CharacterArt.Clip { name = "torch", row = 0, frames = 2, fps = 8f, loop = true },
                new CharacterArt.Clip { name = "brazier", row = 1, frames = 2, fps = 8f, loop = true }
            };
            return art;
        }

        BiomeData MakeBiome(bool painted)
        {
            var biome = ScriptableObject.CreateInstance<BiomeData>();
            _owned.Add(biome);
            if (!painted) return biome;

            var art = ScriptableObject.CreateInstance<EnvironmentArt>();
            _owned.Add(art);
            art.wall = TileSprite();
            art.floorTrim = TileSprite();
            art.platform = TileSprite();
            art.props = PropsArt();
            biome.art = art;
            return biome;
        }

        static List<string> SolidGeometry(BuiltLevel level)
        {
            var list = new List<string>();
            foreach (var col in level.Root.GetComponentsInChildren<BoxCollider2D>())
            {
                string layer = LayerMask.LayerToName(col.gameObject.layer);
                if (layer != GameLayers.GroundName && layer != GameLayers.OneWayName) continue;
                Bounds b = col.bounds;
                list.Add($"{layer} {b.min.x:0.00},{b.min.y:0.00} {b.max.x:0.00},{b.max.y:0.00}");
            }
            list.Sort();
            return list;
        }

        [Test]
        public void PaintedLevelsHaveTheSameSolidGeometryAsGrayboxOnes()
        {
            for (uint seed = 1; seed <= 6; seed++)
            {
                BiomeData gray = MakeBiome(false), painted = MakeBiome(true);
                BuiltLevel a = LevelBuilder.Build(LevelGenerator.Generate(seed, gray.generator), gray, false);
                _spawned.Add(a.Root);
                var ga = SolidGeometry(a);
                Object.DestroyImmediate(a.Root);

                BuiltLevel b = LevelBuilder.Build(LevelGenerator.Generate(seed, painted.generator), painted, false);
                _spawned.Add(b.Root);
                var gb = SolidGeometry(b);

                Assert.AreEqual(ga.Count, gb.Count, $"seed {seed}: same number of solids");
                CollectionAssert.AreEqual(ga, gb, $"seed {seed}: same solids in the same places");
                Object.DestroyImmediate(b.Root);
            }
        }

        [Test]
        public void PaintedRoomsUseTiledSpritesAndGetLitDecor()
        {
            BiomeData painted = MakeBiome(true);
            BuiltLevel level = LevelBuilder.Build(LevelGenerator.Generate(3, painted.generator), painted, false);
            _spawned.Add(level.Root);

            var tiled = level.Root.GetComponentsInChildren<SpriteRenderer>().Where(sr => sr.drawMode == SpriteDrawMode.Tiled).ToList();
            Assert.Greater(tiled.Count, 10, "walls, floors and ledges are tiled sprites");

            var lights = level.Root.GetComponentsInChildren<Light2D>();
            Assert.GreaterOrEqual(lights.Length, level.Chunks.Count * 2, "every room gets at least its two wall torches");
            Assert.IsTrue(lights.All(l => l.GetComponent<TorchFlicker>() != null), "decor lights flicker");
        }

        [Test]
        public void GrayboxLevelsAreUntouched()
        {
            BiomeData gray = MakeBiome(false);
            BuiltLevel level = LevelBuilder.Build(LevelGenerator.Generate(3, gray.generator), gray, false);
            _spawned.Add(level.Root);

            Assert.IsNull(WorldArt.Props);
            Assert.AreEqual(0, level.Root.GetComponentsInChildren<Light2D>().Length, "no art, no lights");
            Assert.IsFalse(level.Root.GetComponentsInChildren<SpriteRenderer>().Any(sr => sr.drawMode == SpriteDrawMode.Tiled));
        }
    }
}
