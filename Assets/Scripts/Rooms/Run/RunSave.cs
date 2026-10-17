using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;
using Roughlike.Items;

namespace Roughlike.Rooms
{
    [Serializable]
    public class ShopSave
    {
        public int cellIndex;
        public List<bool> sold = new List<bool>();
    }

    /// <summary>
    /// Everything needed to continue a run. The level itself is not stored: it is rebuilt from the seed (deterministic),
    /// then this data restores what changed: what you carry, where you are, which rooms are cleared, which chests are open.
    /// </summary>
    [Serializable]
    public class RunSave
    {
        public const int CurrentVersion = 1;
        public const string SaveName = "run";

        public int version = CurrentVersion;
        public long seed;
        public string biome;

        // player
        public float health;
        public int gold, cells, flask;
        public int[] scrolls = new int[3];
        public List<string> mutations = new List<string>();
        public ItemSave[] weapons = { ItemSave.Empty, ItemSave.Empty };
        public ItemSave[] skills = { ItemSave.Empty, ItemSave.Empty };
        public ItemSave amulet = ItemSave.Empty;
        public ItemSave outfit = ItemSave.Empty;

        // position and exploration
        public int roomX, roomY;
        public List<int> visited = new List<int>();         // cell indices

        // world state
        public List<int> clearedRooms = new List<int>();    // cell indices
        public List<int> openedChests = new List<int>();
        public List<int> usedAltars = new List<int>();
        public List<ShopSave> shops = new List<ShopSave>();

        // stats
        public int kills, roomsVisited;
        public float time, damageTaken;

        // shown on the Continue button
        public string summary;

        public string ToJson() => JsonUtility.ToJson(this, true);

        public static bool TryParse(string json, out RunSave save)
        {
            save = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                save = JsonUtility.FromJson<RunSave>(json);
                return save != null && save.version >= 1 && save.version <= CurrentVersion && save.seed != 0;
            }
            catch (Exception) { return false; }
        }

        // ---- file access -----------------------------------------------------------------

        public static bool Exists => SaveService.Exists(SaveName);

        public static bool TryLoad(out RunSave save)
        {
            save = null;
            if (!SaveService.TryRead(SaveName, out string json, t => TryParse(t, out _))) return false;
            return TryParse(json, out save);
        }

        public void Write() => SaveService.Write(SaveName, ToJson());

        public static void Delete() => SaveService.Delete(SaveName);
    }

    public enum SessionMode { None, NewRun, Continue }

    /// <summary>Passes the menu's choice (new run, continue) to the gameplay scene.</summary>
    public static class GameSession
    {
        public const string MenuScene = "MainMenu";
        public const string GameplayScene = "M4_LootRun";
        public const string TutorialScene = "Tutorial";

        public static SessionMode Mode;
        /// <summary>Seed for a new run; 0 means pick one at random.</summary>
        public static uint Seed;

        public static void StartNew(uint seed = 0) { Mode = SessionMode.NewRun; Seed = seed; }
        public static void StartContinue() { Mode = SessionMode.Continue; Seed = 0; }
        public static void Clear() { Mode = SessionMode.None; Seed = 0; }
    }
}
