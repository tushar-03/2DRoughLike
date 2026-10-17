using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>Saving a run mid-way and continuing it: the level is rebuilt from the seed and the save restores your state.</summary>
    public class RunSaveTests
    {
        BiomeData _biome;
        LootDatabase _db;
        MovementConfig _cfg;
        readonly List<Object> _owned = new List<Object>();
        readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            RunSave.Delete();
            _db = DefaultContent.CreateDatabase();
            _biome = ScriptableObject.CreateInstance<BiomeData>();
            _biome.loot = _db;
            _biome.name = "TestBiome";
            var grunt = ScriptableObject.CreateInstance<EnemyDefinition>();
            _biome.enemyPool = new[] { grunt };
            _cfg = ScriptableObject.CreateInstance<MovementConfig>();
            _owned.AddRange(new Object[] { _db, _biome, grunt, _cfg });
        }

        [TearDown]
        public void TearDown()
        {
            RunSave.Delete();
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        (RunManager run, PlayerController pc, PlayerInventory inv, Health health, PlayerDefense def) MakeRun()
        {
            Physics2D.SyncTransforms();
            var pc = PlayerFactory.Build(_cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            PlayerCombatSetup.Attach(pc, null, null);
            var inv = PlayerItemsSetup.Attach(pc);
            pc.autoTick = false;
            _spawned.Add(pc.gameObject);

            var go = new GameObject("Run");
            go.SetActive(false);
            var run = go.AddComponent<RunManager>();
            run.AutoStart = false;
            run.Bind(_biome, pc);
            go.SetActive(true);
            _spawned.Add(go);
            return (run, pc, inv, pc.GetComponent<Health>(), pc.GetComponent<PlayerDefense>());
        }

        [Test]
        public void StartingARunWritesASaveThatContinueCanRead()
        {
            var (run, pc, inv, health, def) = MakeRun();
            Assert.IsFalse(RunSave.Exists);
            run.StartRun(2468);
            Assert.IsTrue(RunSave.Exists);
            Assert.IsTrue(RunSave.TryLoad(out RunSave save));
            Assert.AreEqual(2468, save.seed);
            StringAssert.Contains("Seed", save.summary);
        }

        [Test]
        public void ContinuingRestoresInventoryHealthAndTheStateOfTheWorld()
        {
            var (run, pc, inv, health, def) = MakeRun();
            run.StartRun(8642);

            // play a little: loot, spend, clear a room, open a chest, use an altar, buy from the shop
            inv.AddGold(333);
            inv.AddCells(2);
            inv.AddScroll(StatColor.Ember);
            inv.AddScroll(StatColor.Moss);
            inv.AddMutation(_db.mutations[3]);
            var epic = ItemInstance.ForWeapon(_db.weapons[2], ItemRarity.Epic, 3);
            epic.Cursed = true;
            epic.Affixes.Add(new AffixInstance(System.Array.Find(_db.affixes, a => a.id == "w_burn"), 0.27f));
            epic.Affixes.Add(new AffixInstance(System.Array.Find(_db.affixes, a => a.id == "c_dull"), -0.15f));
            inv.EquipWeapon(0, epic);
            inv.EquipSkill(1, ItemInstance.ForSkill(_db.skills[2], ItemRarity.Rare));
            inv.PickUp(ItemInstance.ForGear(_db.amulets[2], ItemRarity.Epic), Vector2.zero);
            health.TakeHit(new DamageInfo { Amount = 40f, SourceTeam = Team.Enemy, Tags = DamageTags.Melee });

            var tracker = run.Level.Trackers.Find(t => t.Alive > 0);
            Assert.IsNotNull(tracker);
            foreach (var e in run.Level.Enemies.FindAll(x => x != null && x.GetComponentInParent<RoomClearTracker>() == tracker))
                e.GetComponent<Health>().TakeHit(new DamageInfo { Amount = 9999f, SourceTeam = Team.Player });
            Assert.IsTrue(tracker.Cleared);

            var chest = run.Level.Chests[0];
            chest.Interact(inv, pc);
            var altar = run.Level.Altars[0];
            Assert.IsTrue(altar.Choose(inv, 0));
            var stand = run.Level.Shops[0];
            var scrollOffer = stand.Stock.Offers.Find(o => o.Kind == OfferKind.Scroll);
            inv.AddGold(500);
            stand.Stock.TryBuy(scrollOffer, inv, Vector2.zero);

            // walk into another room, then save
            var otherRoom = run.Layout.Cells.Find(c => c.Pos != run.Layout.Start.Pos && c.Role == RoomRole.Combat);
            var chunk = run.Level.Chunks[otherRoom.Pos];
            pc.ResetTo(chunk.PlayerSpawn);
            run.RefreshRoom();
            run.SaveNow();

            int gold = inv.Gold, cells = inv.Cells, flask = inv.FlaskCharges;
            int[] scrolls = (int[])inv.Scrolls.Clone();
            string weapon = inv.Weapons[0].DisplayName;
            float hp = health.Current;
            int visited = run.Visited.Count;
            int clearedIndex = tracker.Cell.Index;
            int mutationCount = inv.Mutations.Count;
            Assert.IsTrue(RunSave.TryLoad(out RunSave save));

            // a different run happens in between, then we continue the old one
            run.StartRun(1111);
            Assert.AreNotEqual(8642u, run.Seed);
            RunSave.Delete();
            run.StartRun(8642, save);

            Assert.AreEqual(8642u, run.Seed);
            Assert.AreEqual(gold, inv.Gold);
            Assert.AreEqual(cells, inv.Cells);
            Assert.AreEqual(flask, inv.FlaskCharges);
            CollectionAssert.AreEqual(scrolls, inv.Scrolls);
            Assert.AreEqual(mutationCount, inv.Mutations.Count);
            Assert.AreEqual(weapon, inv.Weapons[0].DisplayName);
            Assert.AreEqual(ItemRarity.Epic, inv.Weapons[0].Rarity);
            Assert.AreEqual(3, inv.Weapons[0].Tier);
            Assert.IsTrue(inv.Weapons[0].Cursed);
            Assert.AreEqual(2, inv.Weapons[0].Affixes.Count);
            Assert.IsNotNull(inv.Skills[1]);
            Assert.IsNotNull(inv.Amulet);
            Assert.AreEqual(hp, run.PlayerHealth.Current, 0.5f);
            Assert.AreEqual(visited, run.Visited.Count);
            Assert.AreEqual(otherRoom.Pos, run.CurrentRoom);

            // the world remembers what you did
            var restored = run.Level.Trackers.Find(t => t.Cell.Index == clearedIndex);
            Assert.IsTrue(restored.Cleared);
            Assert.AreEqual(0, restored.Chunk.GetComponentsInChildren<EnemyController>(false).Length, "cleared rooms stay cleared");
            Assert.IsTrue(run.Level.Chests[0].Opened);
            Assert.IsTrue(run.Level.Altars[0].Used);
            Assert.IsTrue(run.Level.Shops[0].Stock.Offers.Find(o => o.Kind == OfferKind.Scroll).Sold);

            // and you are standing in the room you saved in
            Assert.AreEqual(otherRoom.Pos, ChunkMetrics.CellAt((Vector2)pc.transform.position + Vector2.up));
        }

        [Test]
        public void DyingOrFinishingTheBiomeDeletesTheSave()
        {
            var (run, pc, inv, health, def) = MakeRun();
            run.StartRun(77);
            Assert.IsTrue(RunSave.Exists);
            def.Tick(5f);
            health.TakeHit(new DamageInfo { Amount = 9999f, SourceTeam = Team.Enemy });
            Assert.AreEqual(RunState.Dead, run.State);
            Assert.IsFalse(RunSave.Exists, "permadeath: nothing to continue");

            run.StartRun(78);
            Assert.IsTrue(RunSave.Exists);
            run.Level.Exit.Trigger();
            Assert.AreEqual(RunState.Complete, run.State);
            Assert.IsFalse(RunSave.Exists);
        }

        [Test]
        public void EnteringANewRoomAutosaves()
        {
            var (run, pc, inv, health, def) = MakeRun();
            run.StartRun(99);
            RunSave.TryLoad(out RunSave first);
            Assert.AreEqual(run.Layout.Start.Pos.x, first.roomX);

            var next = run.Layout.Cells.Find(c => c.Pos != run.Layout.Start.Pos);
            pc.ResetTo(run.Level.Chunks[next.Pos].PlayerSpawn);
            run.RefreshRoom();
            RunSave.TryLoad(out RunSave second);
            Assert.AreEqual(next.Pos.x, second.roomX);
            Assert.AreEqual(next.Pos.y, second.roomY);
        }

        [Test]
        public void StartingFreshAbandonsTheOldSaveAndKeepsASingleSlot()
        {
            var (run, pc, inv, health, def) = MakeRun();
            run.StartRun(500);
            run.StartRun(501);
            Assert.IsTrue(RunSave.TryLoad(out RunSave save));
            Assert.AreEqual(501, save.seed);
        }

        [Test]
        public void AssistModeLowersDamageTaken()
        {
            var (run, pc, inv, health, def) = MakeRun();
            run.StartRun(3);
            var stats = pc.GetComponent<PlayerStats>();
            Assert.AreEqual(1f, stats.DamageTakenMultiplier, 0.001f);

            SettingsService.Current.assistMode = true;
            SettingsService.NotifyChanged(false);
            Assert.AreEqual(0.6f, stats.DamageTakenMultiplier, 0.001f);

            SettingsService.Current.assistMode = false;
            SettingsService.NotifyChanged(false);
            Assert.AreEqual(1f, stats.DamageTakenMultiplier, 0.001f);
        }
    }
}
