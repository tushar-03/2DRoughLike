using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>One animation row in a sheet: how many frames, how fast, and how to paint frame i of n.</summary>
    public sealed class ClipPlan
    {
        public string Name;
        public int Frames;
        public float Fps;
        public bool Loop;
        public Func<int, int, Color32[]> Paint;
    }

    public sealed class CharacterPlan
    {
        public string Name;
        public int FrameW = HumanoidArt.FrameSize, FrameH = HumanoidArt.FrameSize;
        public Vector2 Pivot = new Vector2(140f / HumanoidArt.FrameSize, 34f / HumanoidArt.FrameSize);
        public readonly List<ClipPlan> Clips = new List<ClipPlan>();
        /// <summary>Record hand and shield anchors for every frame (the player's gear is drawn separately at runtime).</summary>
        public bool Anchors;

        public CharacterPlan Add(string name, int frames, float fps, bool loop, Func<int, int, Color32[]> paint)
        {
            Clips.Add(new ClipPlan { Name = name, Frames = frames, Fps = fps, Loop = loop, Paint = paint });
            return this;
        }
    }

    /// <summary>The cast of the game: every character's look and animation list. Edit the specs here and rebuild to restyle everything.</summary>
    public static class CharacterLibrary
    {
        static float T(int i, int n) => n <= 1 ? 0f : (float)i / n;
        static float TEnd(int i, int n) => n <= 1 ? 0f : (float)i / (n - 1);

        static CharacterPlan Humanoid(HumanoidSpec s, bool player)
        {
            var plan = new CharacterPlan { Name = s.Name, Anchors = s.RuntimeGear };
            plan.Add("idle", 6, 6f, true, (i, n) => HumanoidArt.Render(s, "idle", T(i, n)));
            if (player)
            {
                plan.Add("run", 8, 14f, true, (i, n) => HumanoidArt.Render(s, "run", T(i, n)));
                plan.Add("jump", 1, 1f, false, (i, n) => HumanoidArt.Render(s, "jump", 0f));
                plan.Add("fall", 1, 1f, false, (i, n) => HumanoidArt.Render(s, "fall", 0f));
                plan.Add("roll", 6, 16f, true, (i, n) => HumanoidArt.RenderRoll(s, T(i, n)));
                plan.Add("block", 1, 1f, false, (i, n) => HumanoidArt.Render(s, "block", 0f));
                plan.Add("attack", 6, 12f, false, (i, n) => HumanoidArt.Render(s, "player_attack", TEnd(i, n)));
                plan.Add("shoot", 6, 12f, false, (i, n) => HumanoidArt.Render(s, "player_shoot", TEnd(i, n)));
                plan.Add("hurt", 2, 8f, false, (i, n) => HumanoidArt.Render(s, "hurt", TEnd(i, n)));
            }
            else
            {
                plan.Add("walk", 8, 9f, true, (i, n) => HumanoidArt.Render(s, "walk", T(i, n)));
                plan.Add("telegraph", 4, 8f, false, (i, n) => HumanoidArt.Render(s, "telegraph", TEnd(i, n)));
                plan.Add("attack", 3, 12f, false, (i, n) => HumanoidArt.Render(s, "attack", TEnd(i, n)));
                plan.Add("hurt", 2, 8f, false, (i, n) => HumanoidArt.Render(s, "hurt", TEnd(i, n)));
            }
            return plan;
        }

        static CharacterPlan Creature(string name, Func<string, float, Color32[]> paint, bool hasWalk)
        {
            var plan = new CharacterPlan { Name = name };
            plan.Add("idle", 6, 6f, true, (i, n) => paint("idle", T(i, n)));
            if (hasWalk) plan.Add("walk", 8, 9f, true, (i, n) => paint("walk", T(i, n)));
            plan.Add("telegraph", 4, 8f, false, (i, n) => paint("telegraph", TEnd(i, n)));
            plan.Add("attack", 6, 14f, true, (i, n) => paint("attack", T(i, n)));
            plan.Add("hurt", 2, 8f, false, (i, n) => paint("hurt", TEnd(i, n)));
            return plan;
        }

        public static List<CharacterPlan> All()
        {
            var ink = new Color(0.07f, 0.05f, 0.1f);

            var player = new HumanoidSpec
            {
                Name = "BellRinger", Height = 262f, HeadR = 31f, Hood = true, Cloak = true, Bell = true, Weapon = "none", WeaponLen = 100f, RuntimeGear = true,
                Skin = new Color(0.92f, 0.76f, 0.64f), Cloth = new Color(0.9f, 0.86f, 0.76f), Cloth2 = new Color(0.52f, 0.24f, 0.24f),
                Trim = new Color(0.96f, 0.76f, 0.32f), Metal = new Color(0.86f, 0.8f, 0.62f), Glow = new Color(1f, 0.82f, 0.36f),
                Boot = new Color(0.3f, 0.2f, 0.2f), Rim = new Color(1f, 0.88f, 0.6f)
            };
            var grunt = new HumanoidSpec
            {
                Name = "DrownedGrunt", Height = 244f, HeadR = 27f, Bulk = 1.12f, Hunch = 20f, Weapon = "club", WeaponLen = 92f, Mouth = true,
                Skin = new Color(0.46f, 0.64f, 0.6f), Cloth = new Color(0.42f, 0.47f, 0.36f), Cloth2 = new Color(0.26f, 0.3f, 0.26f),
                Trim = new Color(0.66f, 0.38f, 0.22f), Glow = new Color(1f, 0.56f, 0.2f), Boot = new Color(0.22f, 0.2f, 0.18f), Rim = new Color(0.8f, 1f, 0.9f)
            };
            var spitter = new HumanoidSpec
            {
                Name = "DrownedSpitter", Height = 238f, HeadR = 31f, Hunch = 6f, Weapon = "none", Attack = AttackStyle.Spit, Fins = true, Mouth = true,
                Skin = new Color(0.32f, 0.62f, 0.72f), Cloth = new Color(0.24f, 0.4f, 0.52f), Cloth2 = new Color(0.16f, 0.26f, 0.38f),
                Trim = new Color(0.78f, 0.9f, 0.4f), Glow = new Color(0.78f, 1f, 0.36f), Boot = new Color(0.16f, 0.22f, 0.3f), Rim = new Color(0.7f, 1f, 1f)
            };
            var wretch = new HumanoidSpec
            {
                Name = "DrownedWretch", Height = 178f, HeadR = 25f, Hunch = 14f, Weapon = "dagger", WeaponLen = 80f, Mouth = true, Bulk = 0.9f,
                Skin = new Color(0.52f, 0.62f, 0.56f), Cloth = new Color(0.36f, 0.36f, 0.32f), Cloth2 = new Color(0.22f, 0.22f, 0.2f),
                Trim = new Color(0.5f, 0.3f, 0.24f), Glow = new Color(1f, 0.36f, 0.24f), Boot = new Color(0.18f, 0.17f, 0.16f), Rim = new Color(0.9f, 1f, 0.9f)
            };
            var guard = new HumanoidSpec
            {
                Name = "ShellGuard", Height = 262f, HeadR = 29f, Bulk = 1.5f, Helmet = true, Weapon = "mace", WeaponLen = 92f, EnemyShield = true,
                Skin = new Color(0.6f, 0.5f, 0.45f), Cloth = new Color(0.44f, 0.54f, 0.7f), Cloth2 = new Color(0.26f, 0.32f, 0.46f),
                Trim = new Color(0.9f, 0.78f, 0.44f), Metal = new Color(0.62f, 0.72f, 0.82f), Glow = new Color(0.55f, 0.88f, 1f),
                Boot = new Color(0.2f, 0.22f, 0.3f), Rim = new Color(0.85f, 0.95f, 1f)
            };
            var shade = new HumanoidSpec
            {
                Name = "ReliquaryShade", Height = 262f, HeadR = 28f, Hood = true, Robe = true, Floating = true, Weapon = "none", Attack = AttackStyle.Cast, Cloak = false,
                Skin = new Color(0.5f, 0.4f, 0.6f), Cloth = new Color(0.46f, 0.3f, 0.66f), Cloth2 = new Color(0.22f, 0.14f, 0.36f),
                Trim = new Color(0.78f, 0.62f, 0.96f), Glow = new Color(0.9f, 0.52f, 1f), Boot = new Color(0.2f, 0.12f, 0.3f), Rim = new Color(0.9f, 0.75f, 1f)
            };
            var eel = new HumanoidSpec
            {
                Name = "EelArcher", Height = 250f, HeadR = 27f, Fins = true, Mouth = true, Weapon = "bow", Attack = AttackStyle.Shoot,
                Skin = new Color(0.4f, 0.64f, 0.74f), Cloth = new Color(0.26f, 0.44f, 0.62f), Cloth2 = new Color(0.16f, 0.26f, 0.42f),
                Trim = new Color(0.46f, 0.92f, 1f), Glow = new Color(0.5f, 0.96f, 1f), Boot = new Color(0.14f, 0.2f, 0.3f), Rim = new Color(0.75f, 1f, 1f)
            };

            // ---- the Ashen Promenade roster: the same eight behaviours in new bodies
            var footman = new HumanoidSpec
            {
                Name = "AshFootman", Height = 250f, HeadR = 28f, Bulk = 1.18f, Helmet = true, Weapon = "sword", WeaponLen = 88f, Mouth = false,
                Skin = new Color(0.62f, 0.55f, 0.5f), Cloth = new Color(0.58f, 0.26f, 0.2f), Cloth2 = new Color(0.3f, 0.14f, 0.12f),
                Trim = new Color(0.9f, 0.62f, 0.28f), Metal = new Color(0.66f, 0.62f, 0.6f), Glow = new Color(1f, 0.55f, 0.2f),
                Boot = new Color(0.22f, 0.15f, 0.13f), Rim = new Color(1f, 0.8f, 0.55f)
            };
            var imp = new HumanoidSpec
            {
                Name = "EmberImp", Height = 205f, HeadR = 32f, Hunch = 8f, Horns = true, Mouth = true, Weapon = "none", Attack = AttackStyle.Spit,
                Skin = new Color(0.86f, 0.38f, 0.22f), Cloth = new Color(0.4f, 0.14f, 0.12f), Cloth2 = new Color(0.24f, 0.08f, 0.08f),
                Trim = new Color(1f, 0.78f, 0.3f), Glow = new Color(1f, 0.9f, 0.3f), Boot = new Color(0.2f, 0.09f, 0.08f), Rim = new Color(1f, 0.8f, 0.5f)
            };
            var bones = new HumanoidSpec
            {
                Name = "BoneArcher", Height = 255f, HeadR = 27f, Mouth = true, Weapon = "bow", Attack = AttackStyle.Shoot,
                Skin = new Color(0.9f, 0.87f, 0.78f), Cloth = new Color(0.38f, 0.28f, 0.22f), Cloth2 = new Color(0.22f, 0.16f, 0.14f),
                Trim = new Color(0.8f, 0.7f, 0.5f), Glow = new Color(1f, 0.3f, 0.2f), Boot = new Color(0.2f, 0.15f, 0.12f), Rim = new Color(1f, 0.9f, 0.7f)
            };
            var sentinel = new HumanoidSpec
            {
                Name = "RustSentinel", Height = 262f, HeadR = 29f, Bulk = 1.55f, Helmet = true, Weapon = "sword", WeaponLen = 84f, EnemyShield = true,
                Skin = new Color(0.55f, 0.42f, 0.38f), Cloth = new Color(0.5f, 0.3f, 0.22f), Cloth2 = new Color(0.28f, 0.16f, 0.13f),
                Trim = new Color(0.95f, 0.6f, 0.25f), Metal = new Color(0.62f, 0.5f, 0.44f), Glow = new Color(1f, 0.5f, 0.2f),
                Boot = new Color(0.2f, 0.14f, 0.12f), Rim = new Color(1f, 0.78f, 0.5f)
            };
            var priest = new HumanoidSpec
            {
                Name = "AshPriest", Height = 262f, HeadR = 28f, Hood = true, Robe = true, Floating = true, Weapon = "none", Attack = AttackStyle.Cast, Cloak = false,
                Skin = new Color(0.55f, 0.45f, 0.42f), Cloth = new Color(0.62f, 0.24f, 0.2f), Cloth2 = new Color(0.26f, 0.1f, 0.1f),
                Trim = new Color(1f, 0.72f, 0.3f), Glow = new Color(1f, 0.55f, 0.2f), Boot = new Color(0.22f, 0.1f, 0.1f), Rim = new Color(1f, 0.78f, 0.55f)
            };
            var husk = new HumanoidSpec
            {
                Name = "AshHusk", Height = 172f, HeadR = 25f, Hunch = 14f, Weapon = "dagger", WeaponLen = 76f, Mouth = true, Bulk = 0.9f,
                Skin = new Color(0.5f, 0.46f, 0.44f), Cloth = new Color(0.34f, 0.3f, 0.3f), Cloth2 = new Color(0.2f, 0.18f, 0.18f),
                Trim = new Color(0.6f, 0.3f, 0.2f), Glow = new Color(1f, 0.6f, 0.2f), Boot = new Color(0.18f, 0.16f, 0.15f), Rim = new Color(1f, 0.9f, 0.8f)
            };

            // bosses are drawn at 1.7x on a bigger canvas
            const float BossScale = 1.7f;
            var witch = new HumanoidSpec
            {
                Name = "HagOfBells", Height = 262f, HeadR = 30f, Scale = BossScale, Hood = true, Hat = true, Robe = true, Floating = true,
                Weapon = "staff", Attack = AttackStyle.Cast, Cloak = false,
                Skin = new Color(0.55f, 0.7f, 0.5f), Cloth = new Color(0.42f, 0.2f, 0.58f), Cloth2 = new Color(0.2f, 0.1f, 0.32f),
                Trim = new Color(0.55f, 1f, 0.45f), Glow = new Color(0.62f, 1f, 0.42f), Boot = new Color(0.16f, 0.1f, 0.24f), Rim = new Color(0.85f, 0.7f, 1f),
                Metal = new Color(0.55f, 0.4f, 0.3f), Outline = 3.6f
            };
            var warden = new HumanoidSpec
            {
                Name = "RustWarden", Height = 262f, HeadR = 30f, Scale = BossScale, Bulk = 1.75f, Helmet = true, Weapon = "greatsword", WeaponLen = 165f,
                Skin = new Color(0.55f, 0.4f, 0.35f), Cloth = new Color(0.52f, 0.28f, 0.2f), Cloth2 = new Color(0.28f, 0.15f, 0.13f),
                Trim = new Color(0.95f, 0.72f, 0.3f), Metal = new Color(0.72f, 0.7f, 0.74f), Glow = new Color(1f, 0.5f, 0.2f),
                Boot = new Color(0.2f, 0.14f, 0.12f), Rim = new Color(1f, 0.75f, 0.5f), Outline = 3.8f
            };

            var list = new List<CharacterPlan>
            {
                Humanoid(player, true),
                Humanoid(grunt, false),
                Humanoid(spitter, false),
                Humanoid(wretch, false),
                Humanoid(guard, false),
                Humanoid(shade, false),
                Humanoid(eel, false),
                Creature("BellWisp", CreatureArt.Wisp, false),
                Creature("Tideboar", CreatureArt.Boar, true),
                Creature("Bloat", CreatureArt.Bloat, true),
                Humanoid(footman, false),
                Humanoid(imp, false),
                Humanoid(bones, false),
                Humanoid(sentinel, false),
                Humanoid(priest, false),
                Humanoid(husk, false),
                Creature("EmberWisp", CreatureArt.EmberWisp, false),
                Creature("CinderBoar", CreatureArt.CinderBoar, true),
                Creature("AshBomber", CreatureArt.AshBomber, true)
            };

            foreach (var boss in new[] { Humanoid(witch, false), Humanoid(warden, false) })
            {
                int canvas = Mathf.RoundToInt(HumanoidArt.FrameSize * BossScale);
                boss.FrameW = canvas; boss.FrameH = canvas;
                // keep the sheet within 4096 pixels (browsers and phones): 6 frames of 544 across
                foreach (var c in boss.Clips) if (c.Frames > 6) c.Frames = 6;
                list.Add(boss);
            }

            // the guard's shield is a separate prop so it can break
            list[4].Add("shield", 1, 1f, false, (i, n) => CreatureArt.ShieldProp(false));
            list[4].Add("shield_broken", 1, 1f, false, (i, n) => CreatureArt.ShieldProp(true));
            var sentinelPlan = list.Find(p => p.Name == "RustSentinel");
            sentinelPlan.Add("shield", 1, 1f, false, (i, n) => CreatureArt.ShieldProp(false));
            sentinelPlan.Add("shield_broken", 1, 1f, false, (i, n) => CreatureArt.ShieldProp(true));
            _ = ink;

            var props = new CharacterPlan { Name = "Props", FrameW = PropArt.W, FrameH = PropArt.H, Pivot = new Vector2(0.5f, 0f) };
            props.Add("torch", 4, 8f, true, (i, n) => PropArt.Torch(i, n));
            props.Add("brazier", 4, 8f, true, (i, n) => PropArt.Brazier(i, n));
            props.Add("flame", 4, 9f, true, (i, n) => PropArt.Flame(i, n));
            list.Add(props);

            var world = new CharacterPlan { Name = "WorldProps", FrameW = WorldPropArt.W, FrameH = WorldPropArt.H, Pivot = new Vector2(0.5f, WorldPropArt.Floor / (float)WorldPropArt.H) };
            world.Add("door", 4, 6f, true, (i, n) => WorldPropArt.Door(i, n));
            world.Add("chest", 1, 1f, false, (i, n) => WorldPropArt.Chest(false));
            world.Add("chest_open", 1, 1f, false, (i, n) => WorldPropArt.Chest(true));
            world.Add("altar", 1, 1f, false, (i, n) => WorldPropArt.Altar());
            world.Add("merchant", 1, 1f, false, (i, n) => WorldPropArt.Merchant());
            list.Add(world);
            return list;
        }
    }
}
