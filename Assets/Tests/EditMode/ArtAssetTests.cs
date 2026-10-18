using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Rooms;
using Roughlike.Utils;

namespace Roughlike.Tests.EditMode
{
    /// <summary>The generated art is wired up: weapon sprites on every weapon, anchors on the player sheet, depth layers in each biome.</summary>
    public class ArtAssetTests
    {
        [TestCase("RustedDagger")]
        [TestCase("BellringerSword")]
        [TestCase("TideHammer")]
        [TestCase("Boathook")]
        [TestCase("TowerShield")]
        [TestCase("Buckler")]
        [TestCase("HuntersBow")]
        [TestCase("TwinBlades")]
        [TestCase("Greatsword")]
        public void EveryStartingWeaponHasPaintedArt(string asset)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/Data/Weapons/{asset}.asset");
            Assert.NotNull(w, asset);
            Assert.NotNull(w.sprite, asset + " sprite");
            Assert.Greater(w.heldScale, 0.2f);
            // melee art is a blade pointing up, taller than it is wide; shields are painted face-on
            if (w.kind == WeaponKind.Melee && !w.dual) Assert.Greater(w.sprite.rect.height, w.sprite.rect.width * 1.6f, asset + " is long and thin");
        }

        [Test]
        public void PlayerSheetStoresHandAnchorsForEveryPosedFrame()
        {
            var art = AssetDatabase.LoadAssetAtPath<CharacterArt>("Assets/Art/Characters/BellRinger.asset");
            Assert.NotNull(art);
            foreach (var clip in art.clips)
            {
                if (clip.name == "roll") continue;   // the roll tucks the character up; gear is hidden
                Assert.NotNull(clip.anchors, clip.name);
                Assert.AreEqual(clip.frames, clip.anchors.Length, clip.name);
                foreach (var a in clip.anchors) Assert.IsTrue(a.valid, clip.name);
            }
            Assert.IsTrue(art.TryAnchor("block", 0, out var block));
            Assert.IsTrue(block.shieldUp, "the block pose raises the shield");
            Assert.IsTrue(art.TryAnchor("idle", 0, out var idle));
            Assert.IsFalse(idle.shieldUp);
            Assert.IsFalse(art.TryAnchor("roll", 0, out _));
            Assert.IsTrue(art.Has("shoot"), "the bow has its own draw-and-release animation");
            Assert.IsTrue(art.TryAnchor("attack", 3, out var swing));
            Assert.AreNotEqual(swing.hand, swing.handB, "the off hand is tracked for dual blades");
        }

        [TestCase("Assets/Art/Environment/EmberCatacombsArt.asset")]
        [TestCase("Assets/Art/Environment/AshenPromenadeArt.asset")]
        public void EveryBiomeHasBackgroundAndForegroundParallax(string path)
        {
            var art = AssetDatabase.LoadAssetAtPath<EnvironmentArt>(path);
            Assert.NotNull(art, path);
            int back = 0, front = 0;
            foreach (var l in art.backdrop)
            {
                Assert.NotNull(l.sprite, l.name);
                if (l.follow < 0f) { front++; Assert.Greater(l.sortingOrder, 10, l.name + " draws over the action"); }
                else back++;
            }
            Assert.GreaterOrEqual(back, 7, "many background planes");
            Assert.GreaterOrEqual(front, 3, "foreground planes in front of the player");
            Assert.Greater(art.moteCount, 0, "drifting motes");
        }

        [Test]
        public void FourLoadoutsAreOfferedAndUseRealWeapons()
        {
            var db = AssetDatabase.LoadAssetAtPath<Roughlike.Items.LootDatabase>("Assets/Data/LootDatabase.asset");
            Assert.NotNull(db);
            Assert.AreEqual(4, db.loadouts.Length);
            foreach (var l in db.loadouts)
            {
                Assert.NotNull(l.slot1, l.id);
                Assert.NotNull(l.slot2, l.id);
                Assert.IsFalse(string.IsNullOrEmpty(l.description), l.id);
            }
            CollectionAssert.AreEquivalent(new[] { "bow", "dual", "greatsword", "sword_shield" }, System.Array.ConvertAll(db.loadouts, l => l.id));
        }

        [Test]
        public void EachBiomeHasItsOwnEnemies()
        {
            var a = AssetDatabase.LoadAssetAtPath<BiomeData>("Assets/Data/Biomes/EmberCatacombs.asset");
            var b = AssetDatabase.LoadAssetAtPath<BiomeData>("Assets/Data/Biomes/AshenPromenade.asset");
            Assert.NotNull(a); Assert.NotNull(b);
            Assert.AreEqual(8, a.enemyPool.Length);
            Assert.AreEqual(8, b.enemyPool.Length);
            foreach (var e in b.enemyPool)
            {
                CollectionAssert.DoesNotContain(a.enemyPool, e, e.name + " belongs to the second biome only");
                Assert.NotNull(e.art, e.name + " has its own painted sheet");
            }
            var archetypes = System.Array.ConvertAll(b.enemyPool, e => e.archetype);
            CollectionAssert.AreEquivalent(System.Array.ConvertAll(a.enemyPool, e => e.archetype), archetypes, "the same eight behaviours in new bodies");
        }
    }
}
