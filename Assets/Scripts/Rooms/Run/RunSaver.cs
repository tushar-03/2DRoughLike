using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Items;

namespace Roughlike.Rooms
{
    /// <summary>Turns a live run into a <see cref="RunSave"/> and back. The level is rebuilt from the seed, then the save restores what changed.</summary>
    public static class RunSaver
    {
        public static RunSave Capture(RunManager run)
        {
            var inv = run.Inventory;
            var layout = run.Layout;
            var save = new RunSave
            {
                seed = run.Seed,
                biome = run.Biome != null ? run.Biome.name : "",
                levelIndex = run.LevelIndex,
                difficulty = (int)Roughlike.Core.DifficultyService.Current,
                health = run.PlayerHealth.Current,
                roomX = run.CurrentRoom.x,
                roomY = run.CurrentRoom.y,
                kills = run.Stats.Kills,
                bosses = run.Stats.Bosses,
                roomsVisited = run.Stats.RoomsVisited,
                time = run.Stats.Time,
                damageTaken = run.Stats.DamageTaken
            };

            if (inv != null)
            {
                save.gold = inv.Gold;
                save.cells = inv.Cells;
                save.flask = inv.FlaskCharges;
                for (int i = 0; i < 3; i++) save.scrolls[i] = inv.Scrolls[i];
                foreach (var m in inv.Mutations) save.mutations.Add(m.name);
                for (int i = 0; i < 2; i++)
                {
                    save.weapons[i] = ItemSave.From(inv.Weapons[i]);
                    save.skills[i] = ItemSave.From(inv.Skills[i]);
                }
                save.amulet = ItemSave.From(inv.Amulet);
                save.outfit = ItemSave.From(inv.Outfit);
            }

            foreach (var pos in run.Visited) if (layout.TryGet(pos, out var c)) save.visited.Add(c.Index);

            foreach (var t in run.Level.Trackers) if (t.Cleared) save.clearedRooms.Add(t.Cell.Index);
            foreach (var c in run.Level.Chests) if (c.Opened) save.openedChests.Add(c.CellIndex);
            foreach (var a in run.Level.Altars) if (a.Used) save.usedAltars.Add(a.CellIndex);
            foreach (var s in run.Level.Shops)
            {
                var shop = new ShopSave { cellIndex = s.CellIndex };
                foreach (var o in s.Stock.Offers) shop.sold.Add(o.Sold);
                save.shops.Add(shop);
            }

            // after a boss: the save is the start of the next area (health full, a fresh level), or, after the last boss, the open arena
            if (run.BossDefeated)
            {
                save.pendingItem = ItemSave.From(run.PendingBossItem);
                save.health = run.PlayerHealth.Max;
                if (!run.IsFinalLevel)
                {
                    save.levelIndex = run.LevelIndex + 1;
                    save.biome = "";
                    save.visited.Clear(); save.clearedRooms.Clear(); save.openedChests.Clear(); save.usedAltars.Clear(); save.shops.Clear();
                    save.roomX = save.roomY = int.MinValue;     // not a room of the new level: Apply starts at the entrance
                    save.summary = $"Seed {Roughlike.Core.SeedCode.Encode(run.Seed)}  -  next area unlocked  -  {run.Stats.Kills} kills  -  {save.gold} gold";
                    return save;
                }
                save.bossDefeated = true;
            }

            string room = layout.TryGet(run.CurrentRoom, out var cur) ? cur.Role.ToString() : "?";
            save.summary = $"Seed {Roughlike.Core.SeedCode.Encode(run.Seed)}  -  {run.Stats.RoomsVisited} rooms explored  -  {run.Stats.Kills} kills  -  {room} room  -  {save.gold} gold";
            return save;
        }

        /// <summary>Restores a save onto a freshly built level (same seed). Call after <c>StartRun</c> placed the player.</summary>
        public static void Apply(RunManager run, RunSave save)
        {
            var inv = run.Inventory;
            var db = run.Biome != null ? run.Biome.loot : null;
            var layout = run.Layout;

            if (inv != null && db != null)
            {
                var muts = new List<MutationData>();
                foreach (var name in save.mutations)
                {
                    var m = Array.Find(db.mutations, x => x != null && x.name == name);
                    if (m != null) muts.Add(m);
                }
                inv.RestoreState(
                    new[] { save.weapons[0].ToInstance(db), save.weapons[1].ToInstance(db) },
                    new[] { save.skills[0].ToInstance(db), save.skills[1].ToInstance(db) },
                    save.amulet.ToInstance(db), save.outfit.ToInstance(db),
                    muts, save.scrolls, save.gold, save.cells, save.flask);
            }

            // world state
            foreach (int idx in save.clearedRooms)
            {
                var t = run.Level.Trackers.Find(x => x.Cell.Index == idx);
                if (t != null) t.RestoreCleared();
            }
            foreach (int idx in save.openedChests)
            {
                var c = run.Level.Chests.Find(x => x.CellIndex == idx);
                if (c != null) c.RestoreOpened();
            }
            foreach (int idx in save.usedAltars)
            {
                var a = run.Level.Altars.Find(x => x.CellIndex == idx);
                if (a != null) a.RestoreUsed();
            }
            foreach (var s in save.shops)
            {
                var stand = run.Level.Shops.Find(x => x.CellIndex == s.cellIndex);
                if (stand == null) continue;
                for (int i = 0; i < stand.Stock.Offers.Count && i < s.sold.Count; i++) stand.Stock.Offers[i].Sold = s.sold[i];
            }

            // exploration and stats
            run.Visited.Clear();
            foreach (int idx in save.visited)
                if (idx >= 0 && idx < layout.Cells.Count) run.Visited.Add(layout.Cells[idx].Pos);

            var room = new Vector2Int(save.roomX, save.roomY);
            if (!layout.Contains(room)) room = layout.Start.Pos;
            run.Visited.Add(room);
            run.CurrentRoom = room;

            run.Stats = new RunStats
            {
                Kills = save.kills,
                Bosses = save.bosses,
                RoomsVisited = Mathf.Max(save.roomsVisited, run.Visited.Count),
                Time = save.time,
                DamageTaken = save.damageTaken
            };

            run.BossDefeated = save.bossDefeated;
            // the final boss was already beaten: the arena is open and the exit is waiting
            if (save.bossDefeated && run.Level.Boss != null) run.Level.Boss.RestoreDefeated();
            // the reward that was still on the floor comes back at the player's feet
            if (db != null && save.pendingItem != null && save.pendingItem.kind >= 0)
            {
                var pending = save.pendingItem.ToInstance(db);
                if (pending != null)
                {
                    Vector2 at = run.Level.Chunks.TryGetValue(room, out var dropChunk) ? (Vector2)dropChunk.PlayerSpawn : (Vector2)run.Level.PlayerSpawn;
                    LootFactory.DropItem(pending, at + new Vector2(2.5f, 0f));
                }
            }

            // put the player at the entrance of the room they saved in, with the health they had
            run.PlayerHealth.SetCurrent(Mathf.Max(1f, save.health));
            if (run.Level.Chunks.TryGetValue(room, out var chunk)) run.Player.ResetTo(chunk.PlayerSpawn);
        }
    }
}
