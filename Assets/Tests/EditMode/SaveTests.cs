using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Roughlike.Core;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;

namespace Roughlike.Tests.EditMode
{
    /// <summary>Save files, settings and input rebinding: persistence must be safe against crashes and corruption.</summary>
    public class SaveTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "roughlike-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            SaveService.SetDirectory(_dir);
            SettingsService.Unload();
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch { }
            SettingsService.Unload();
            GameInput.ResetBindingsForTests();
        }

        // ------------------------------------------------------------------ SaveService

        [Test]
        public void WriteThenReadRoundTripsAndLeavesNoTempFile()
        {
            Assert.IsFalse(SaveService.Exists("slot"));
            SaveService.Write("slot", "{\"a\":1}");
            Assert.IsTrue(SaveService.Exists("slot"));
            Assert.IsTrue(SaveService.TryRead("slot", out string json));
            Assert.AreEqual("{\"a\":1}", json);
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "slot.tmp")), "temp file is cleaned up");
        }

        [Test]
        public void SecondWriteKeepsTheFirstAsBackupAndCorruptionFallsBack()
        {
            SaveService.Write("slot", "{\"v\":1}");
            SaveService.Write("slot", "{\"v\":2}");
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "slot.bak")));

            File.WriteAllText(Path.Combine(_dir, "slot.json"), "this is not json");
            Assert.IsTrue(SaveService.TryRead("slot", out string json, t => t.StartsWith("{")));
            Assert.AreEqual("{\"v\":1}", json, "fell back to the last good version");

            File.WriteAllText(Path.Combine(_dir, "slot.json"), "");
            Assert.IsTrue(SaveService.TryRead("slot", out json));
            Assert.AreEqual("{\"v\":1}", json, "an empty file is treated as missing");
        }

        [Test]
        public void MissingFilesAndDeleteBehave()
        {
            Assert.IsFalse(SaveService.TryRead("nothing", out _));
            SaveService.Write("slot", "{}");
            SaveService.Write("slot", "{}");
            SaveService.Delete("slot");
            Assert.IsFalse(SaveService.Exists("slot"));
            SaveService.Delete("slot");   // deleting twice is fine
        }

        // ------------------------------------------------------------------ settings

        [Test]
        public void SettingsDefaultsAreSensibleAndSanitiseClamps()
        {
            var s = new GameSettings();
            Assert.AreEqual(1f, s.master);
            Assert.IsTrue(s.damageNumbers);
            Assert.IsFalse(s.assistMode);
            Assert.IsFalse(s.toggleBlock);

            s.master = 4f; s.screenShake = -2f; s.fullscreenMode = 9; s.resolutionWidth = -5;
            s.Sanitise();
            Assert.AreEqual(1f, s.master);
            Assert.AreEqual(0f, s.screenShake);
            Assert.AreEqual(2, s.fullscreenMode);
            Assert.AreEqual(0, s.resolutionWidth);
        }

        [Test]
        public void SettingsPersistAcrossLoadsAndNotifyListeners()
        {
            int changes = 0;
            System.Action onChanged = () => changes++;
            SettingsService.Changed += onChanged;
            try
            {
                SettingsService.Current.music = 0.25f;
                SettingsService.Current.toggleBlock = true;
                SettingsService.Current.damageNumbers = false;
                SettingsService.NotifyChanged();
                Assert.AreEqual(1, changes);

                SettingsService.Unload();     // simulate a restart
                Assert.AreEqual(0.25f, SettingsService.Current.music, 0.0001f);
                Assert.IsTrue(SettingsService.Current.toggleBlock);
                Assert.IsFalse(SettingsService.Current.damageNumbers);
            }
            finally { SettingsService.Changed -= onChanged; }
        }

        [Test]
        public void CorruptSettingsFileFallsBackToDefaults()
        {
            File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ nope");
            SettingsService.Unload();
            Assert.AreEqual(1f, SettingsService.Current.master);
        }

        [Test]
        public void ResettingSettingsKeepsKeyBindings()
        {
            SettingsService.Current.master = 0.1f;
            SettingsService.Current.bindingOverrides = "{\"bindings\":[]}";
            SettingsService.ResetToDefaults();
            Assert.AreEqual(1f, SettingsService.Current.master);
            Assert.AreEqual("{\"bindings\":[]}", SettingsService.Current.bindingOverrides);
        }

        // ------------------------------------------------------------------ input rebinding

        [Test]
        public void KeyboardAndGamepadBindingsAreFoundByGroup()
        {
            foreach (string action in GameInput.RebindableActions)
            {
                var a = GameInput.Map[action];
                Assert.GreaterOrEqual(GameInput.FindBinding(a, GameInput.KeyboardGroup), 0, action + " has a keyboard binding");
                Assert.GreaterOrEqual(GameInput.FindBinding(a, GameInput.GamepadGroup), 0, action + " has a gamepad binding");
            }
            Assert.AreEqual("Space", GameInput.DisplayFor(GameInput.Jump, GameInput.KeyboardGroup));
        }

        [Test]
        public void RebindingRoundTripsThroughSettingsJson()
        {
            Assert.AreEqual("", GameInput.ExportOverrides(), "no overrides by default");

            var jump = GameInput.Jump;
            int index = GameInput.FindBinding(jump, GameInput.KeyboardGroup);
            jump.ApplyBindingOverride(index, "<Keyboard>/z");
            Assert.AreEqual("Z", GameInput.DisplayFor(jump, GameInput.KeyboardGroup));

            string json = GameInput.ExportOverrides();
            Assert.IsNotEmpty(json);

            GameInput.ResetBindingsForTests();
            Assert.AreEqual("Space", GameInput.DisplayFor(GameInput.Jump, GameInput.KeyboardGroup));

            GameInput.ApplyOverrides(json);
            Assert.AreEqual("Z", GameInput.DisplayFor(GameInput.Jump, GameInput.KeyboardGroup));
        }

        // ------------------------------------------------------------------ item and run saves

        [Test]
        public void ItemsSurviveASaveRoundTripWithAffixesRarityTierAndCurse()
        {
            var db = DefaultContent.CreateDatabase();
            var rng = new SeededRng(12);
            for (int i = 0; i < 60; i++)
            {
                var item = LootRoller.Roll(db, rng, 0.9f, null, 0.7f);
                item.Tier = 1 + (i % 4);
                string json = JsonUtility.ToJson(ItemSave.From(item));
                var back = JsonUtility.FromJson<ItemSave>(json).ToInstance(db);

                Assert.IsNotNull(back);
                Assert.AreEqual(item.Kind, back.Kind);
                Assert.AreEqual(item.BaseName, back.BaseName);
                Assert.AreEqual(item.Rarity, back.Rarity);
                Assert.AreEqual(item.Tier, back.Tier);
                Assert.AreEqual(item.Cursed, back.Cursed);
                Assert.AreEqual(item.Affixes.Count, back.Affixes.Count);
                for (int a = 0; a < item.Affixes.Count; a++)
                {
                    Assert.AreEqual(item.Affixes[a].data.id, back.Affixes[a].data.id);
                    Assert.AreEqual(item.Affixes[a].value, back.Affixes[a].value, 0.0001f);
                }
            }
        }

        [Test]
        public void EmptySlotsAndMissingContentLoadAsNull()
        {
            var db = DefaultContent.CreateDatabase();
            Assert.IsNull(ItemSave.Empty.ToInstance(db));
            Assert.IsNull(ItemSave.From(null).ToInstance(db));
            var ghost = new ItemSave { kind = (int)ItemKind.Weapon, baseName = "RemovedInAnUpdate" };
            Assert.IsNull(ghost.ToInstance(db), "a weapon that no longer exists is dropped, not a crash");
        }

        [Test]
        public void RunSaveJsonRoundTripsAndRejectsGarbage()
        {
            var s = new RunSave { seed = 12345, health = 77f, gold = 31 };
            s.visited.Add(3); s.clearedRooms.Add(2);
            s.shops.Add(new ShopSave { cellIndex = 4, sold = { true, false } });
            s.weapons[1] = new ItemSave { kind = 0, baseName = "TideHammer", rarity = 2 };

            Assert.IsTrue(RunSave.TryParse(s.ToJson(), out RunSave back));
            Assert.AreEqual(12345, back.seed);
            Assert.AreEqual(77f, back.health);
            CollectionAssert.AreEqual(new[] { 3 }, back.visited);
            Assert.AreEqual("TideHammer", back.weapons[1].baseName);
            Assert.IsTrue(back.shops[0].sold[0]);

            Assert.IsFalse(RunSave.TryParse("garbage", out _));
            Assert.IsFalse(RunSave.TryParse("", out _));
            Assert.IsFalse(RunSave.TryParse("{\"seed\":0}", out _), "a save without a seed is useless");
        }

        [Test]
        public void RunSaveFilesUseBackupsToo()
        {
            var a = new RunSave { seed = 111 };
            var b = new RunSave { seed = 222 };
            a.Write();
            b.Write();
            File.WriteAllText(Path.Combine(_dir, "run.json"), "{ broken");
            Assert.IsTrue(RunSave.TryLoad(out RunSave loaded));
            Assert.AreEqual(111, loaded.seed, "fell back to the backup");
            RunSave.Delete();
            Assert.IsFalse(RunSave.Exists);
        }
    }
}
