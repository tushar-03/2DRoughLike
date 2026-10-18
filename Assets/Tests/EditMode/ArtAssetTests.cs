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
            Assert.IsTrue(art.TryAnchor("block_tower", 3, out var block));
            Assert.IsTrue(block.shieldUp, "the block pose raises the shield");
            Assert.IsTrue(art.TryAnchor("idle", 0, out var idle));
            Assert.IsFalse(idle.shieldUp);
            Assert.IsFalse(art.TryAnchor("roll", 0, out _));
            Assert.IsTrue(art.Has("shoot"), "the bow has its own draw-and-release animation");
            foreach (var clip in new[] { "crawl", "block_buckler", "parry", "slash1", "slash2", "overhead", "jab", "jab2", "flurry", "dual_a", "dual_b", "dual_cross", "whirl", "smash", "slam", "uppercut", "great_sweep", "great_overhead", "great_slash3", "poke", "poke_b", "sweep", "shoot_b", "shoot_c" })
                Assert.IsTrue(art.Has(clip), clip + " is reachable through the companion sheets");
            Assert.IsTrue(art.TryAnchor("whirl", 4, out var whirl));
            Assert.AreNotEqual(whirl.angle, whirl.angleB, "the two swords of a spin point different ways");
            foreach (var sheet in art.companions) Assert.LessOrEqual(sheet.sheet.height, 4096, sheet.name + " fits browsers");
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
            Assert.GreaterOrEqual(front, 2, "foreground planes in front of the player");
            foreach (var l in art.backdrop) if (l.follow < 0f) StringAssert.DoesNotContain("column", l.name.ToLower(), "no big dark columns drawn over the action");
            Assert.Greater(art.moteCount, 0, "drifting motes");
        }

        [TestCase("RustedDagger", "jab", "jab2", "flurry")]
        [TestCase("BellringerSword", "slash1", "slash2", "overhead")]
        [TestCase("TideHammer", "smash", "slam", "uppercut")]
        [TestCase("Boathook", "poke", "poke_b", "sweep")]
        [TestCase("TwinBlades", "dual_a", "dual_b", "dual_cross")]
        [TestCase("Greatsword", "great_sweep", "great_overhead", "great_slash3")]
        [TestCase("HuntersBow", "shoot", "shoot_b", "shoot_c")]
        public void EveryComboStepHasItsOwnAnimation(string asset, string a, string b, string c)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/Data/Weapons/{asset}.asset");
            var art = AssetDatabase.LoadAssetAtPath<CharacterArt>("Assets/Art/Characters/BellRinger.asset");
            Assert.AreEqual(a, w.steps[0].clip);
            Assert.AreEqual(b, w.steps[1].clip);
            if (c != null) Assert.AreEqual(c, w.steps[2].clip);
            foreach (var step in w.steps) Assert.IsTrue(art.Has(step.clip), asset + " " + step.name + " plays " + step.clip);
            Assert.GreaterOrEqual(w.steps.Length, 3, asset + " has at least three attacks that flow into each other");
            Assert.AreEqual(w.steps.Length, new System.Collections.Generic.HashSet<string>(System.Array.ConvertAll(w.steps, x => x.clip)).Count, asset + " uses a different animation for every hit");
        }

        [Test]
        public void ShieldsRaiseWithTheirOwnAnimation()
        {
            Assert.AreEqual("block_tower", AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/TowerShield.asset").blockClip);
            Assert.AreEqual("block_buckler", AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/Buckler.asset").blockClip);
        }

        [Test]
        public void WebBuildIncludesEverySceneTheGameLoads()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Editor/WebBuild.cs");
            foreach (var scene in new[] { "MainMenu", "M4_LootRun", "Tutorial", "Loadout" })
                StringAssert.Contains($"Assets/Scenes/{scene}.unity", src, scene + " is in the web build");
            var inBuild = System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
            foreach (var scene in new[] { Roughlike.Rooms.GameSession.MenuScene, Roughlike.Rooms.GameSession.GameplayScene, Roughlike.Rooms.GameSession.TutorialScene, Roughlike.Rooms.GameSession.LoadoutScene })
                CollectionAssert.Contains(inBuild, $"Assets/Scenes/{scene}.unity", scene + " is in the build settings");
        }

        [Test]
        public void EveryItemSkillAndMutationHasAPaintedIcon()
        {
            Roughlike.Utils.GameIcons.Reload();
            var db = AssetDatabase.LoadAssetAtPath<Roughlike.Items.LootDatabase>("Assets/Data/LootDatabase.asset");
            foreach (var sk in db.skills) Assert.IsTrue(Roughlike.Utils.GameIcons.Has("skill_" + sk.name), sk.name);
            foreach (var g in db.amulets) Assert.IsTrue(Roughlike.Utils.GameIcons.Has("gear_" + g.name), g.name);
            foreach (var g in db.outfits) Assert.IsTrue(Roughlike.Utils.GameIcons.Has("gear_" + g.name), g.name);
            foreach (var m in db.mutations) Assert.IsTrue(Roughlike.Utils.GameIcons.Has("mut_" + m.name), m.name);
            foreach (var clip in new[] { "coin", "cell", "scroll_Ember", "scroll_Brine", "scroll_Moss", "flask_full", "flask_empty", "trap_set" })
                Assert.IsTrue(Roughlike.Utils.GameIcons.Has(clip), clip);
            Assert.AreEqual(6, Roughlike.Utils.GameIcons.Frames("coin").Length, "the coin spins");
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
