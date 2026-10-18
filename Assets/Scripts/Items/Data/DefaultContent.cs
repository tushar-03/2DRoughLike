using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;

namespace Roughlike.Items
{
    /// <summary>
    /// The shipped weapons, skills, gear, affixes and mutations, defined in code as the single source of truth.
    /// The editor builder saves them as ScriptableObject assets (which designers then tune); tests build them in memory.
    /// </summary>
    public static class DefaultContent
    {
        static T Make<T>(string name) where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            o.name = name;
            return o;
        }

        /// <summary>Assigns an animation clip to each step of a combo, in order.</summary>
        static WeaponData Clips(WeaponData w, params string[] clips)
        {
            for (int i = 0; i < w.steps.Length && i < clips.Length; i++) w.steps[i].clip = clips[i];
            return w;
        }

        static AttackStep Step(string name, float dmg, float windup, float active, float recovery, float chain,
            Vector2 offset, Vector2 size, Vector2 kb, float stagger, float hitStop, float shake, float lunge, float moveScale)
            => new AttackStep
            {
                name = name, damage = dmg, windup = windup, active = active, recovery = recovery, chainFraction = chain,
                hitOffset = offset, hitSize = size, knockback = kb, stagger = stagger, hitStop = hitStop, shake = shake,
                lunge = lunge, moveScale = moveScale
            };

        // ------------------------------------------------------------------ weapons

        public static WeaponData Dagger()
        {
            var w = Make<WeaponData>("RustedDagger");
            w.displayName = "Rusted Dagger";
            w.tint = new Color(0.75f, 0.9f, 1f);
            w.affinity = StatColor.Brine;
            w.comboResetTime = 0.4f;
            w.description = "Quick and light. Three fast jabs, the last a flurry.";
            w.steps = new[]
            {
                Step("Jab", 7f, 0.03f, 0.06f, 0.10f, 0.3f, new Vector2(0.75f, 0.85f), new Vector2(1.3f, 1.0f), new Vector2(3f, 1.5f), 5f, 0.035f, 0.12f, 3.5f, 0.6f),
                Step("Jab", 7f, 0.03f, 0.06f, 0.10f, 0.3f, new Vector2(0.75f, 0.85f), new Vector2(1.3f, 1.0f), new Vector2(3f, 1.5f), 5f, 0.035f, 0.12f, 3.5f, 0.6f),
                Step("Flurry", 11f, 0.05f, 0.07f, 0.22f, 0.45f, new Vector2(0.85f, 0.85f), new Vector2(1.6f, 1.1f), new Vector2(6f, 3f), 9f, 0.06f, 0.22f, 5f, 0.5f)
            };
            return Clips(w, "jab", "jab2", "flurry");
        }

        public static WeaponData Sword()
        {
            var w = Make<WeaponData>("BellringerSword");
            w.displayName = "Bellringer's Sword";
            w.tint = new Color(1f, 0.9f, 0.55f);
            w.affinity = StatColor.Ember;
            w.comboResetTime = 0.5f;
            w.description = "A balanced blade that tolls on every hit.";
            w.steps = new[]
            {
                Step("Slash", 14f, 0.09f, 0.08f, 0.20f, 0.4f, new Vector2(1.0f, 0.9f), new Vector2(1.9f, 1.3f), new Vector2(5f, 2f), 9f, 0.06f, 0.20f, 3f, 0.35f),
                Step("Backslash", 14f, 0.08f, 0.08f, 0.22f, 0.4f, new Vector2(1.0f, 0.9f), new Vector2(1.9f, 1.3f), new Vector2(5f, 2f), 9f, 0.06f, 0.20f, 3f, 0.35f),
                Step("Cleave", 24f, 0.14f, 0.10f, 0.38f, 0.5f, new Vector2(1.1f, 0.95f), new Vector2(2.3f, 1.5f), new Vector2(9f, 5f), 20f, 0.10f, 0.40f, 5f, 0.3f)
            };
            return Clips(w, "slash1", "slash2", "overhead");
        }

        public static WeaponData Hammer()
        {
            var w = Make<WeaponData>("TideHammer");
            w.displayName = "Tide Hammer";
            w.tint = new Color(1f, 0.55f, 0.35f);
            w.affinity = StatColor.Ember;
            w.comboResetTime = 0.6f;
            w.description = "Slow, heavy, and it staggers everything it touches.";
            w.steps = new[]
            {
                Step("Smash", 30f, 0.28f, 0.10f, 0.50f, 0.55f, new Vector2(1.1f, 0.8f), new Vector2(2.0f, 1.6f), new Vector2(10f, 6f), 35f, 0.14f, 0.55f, 2f, 0.2f),
                Step("Slam", 46f, 0.40f, 0.12f, 0.70f, 0.6f, new Vector2(1.2f, 0.8f), new Vector2(2.4f, 1.8f), new Vector2(12f, 8f), 50f, 0.18f, 0.7f, 2f, 0.15f),
                Step("Upswing", 38f, 0.34f, 0.12f, 0.62f, 0.55f, new Vector2(1.0f, 1.3f), new Vector2(2.2f, 2.4f), new Vector2(7f, 13f), 40f, 0.16f, 0.6f, 2f, 0.2f)
            };
            return Clips(w, "smash", "slam", "uppercut");
        }

        public static WeaponData Boathook()
        {
            var w = Make<WeaponData>("Boathook");
            w.displayName = "Boathook";
            w.tint = new Color(0.7f, 1f, 0.8f);
            w.affinity = StatColor.Brine | StatColor.Moss;
            w.comboResetTime = 0.45f;
            w.description = "Long reach. Keep your distance and poke.";
            w.steps = new[]
            {
                Step("Poke", 10f, 0.08f, 0.07f, 0.16f, 0.35f, new Vector2(1.5f, 0.9f), new Vector2(2.8f, 0.7f), new Vector2(4f, 1f), 6f, 0.04f, 0.14f, 1.5f, 0.5f),
                Step("Poke", 10f, 0.08f, 0.07f, 0.16f, 0.35f, new Vector2(1.5f, 0.9f), new Vector2(2.8f, 0.7f), new Vector2(4f, 1f), 6f, 0.04f, 0.14f, 1.5f, 0.5f),
                Step("Hook", 16f, 0.12f, 0.09f, 0.30f, 0.45f, new Vector2(1.7f, 0.9f), new Vector2(3.2f, 0.9f), new Vector2(8f, 2f), 14f, 0.07f, 0.25f, 2.5f, 0.4f)
            };
            return Clips(w, "poke", "poke_b", "sweep");
        }

        public static WeaponData Bow()
        {
            var w = Make<WeaponData>("HuntersBow");
            w.displayName = "Hunter's Bow";
            w.kind = WeaponKind.Bow;
            w.tint = new Color(0.95f, 0.85f, 0.55f);
            w.affinity = StatColor.Moss;
            w.comboResetTime = 0.5f;
            w.projectileSpeed = 28f;
            w.description = "Shoot from a distance. Two quick arrows, then a heavy shot that passes through enemies.";
            w.steps = new[]
            {
                Step("Shot", 11f, 0.2f, 0.04f, 0.2f, 0.45f, new Vector2(1f, 1.1f), Vector2.one, new Vector2(3f, 1f), 8f, 0.03f, 0.08f, 0f, 0.5f),
                Step("Shot", 11f, 0.2f, 0.04f, 0.2f, 0.45f, new Vector2(1f, 1.1f), Vector2.one, new Vector2(3f, 1f), 8f, 0.03f, 0.08f, 0f, 0.5f),
                Step("Heavy Shot", 24f, 0.4f, 0.05f, 0.34f, 0.5f, new Vector2(1f, 1.1f), Vector2.one, new Vector2(8f, 2f), 22f, 0.07f, 0.2f, -1.5f, 0.3f)
            };
            return Clips(w, "shoot", "shoot_b", "shoot_c");
        }

        public static WeaponData TwinBlades()
        {
            var w = Make<WeaponData>("TwinBlades");
            w.displayName = "Twin Blades";
            w.dual = true;
            w.tint = new Color(0.75f, 0.88f, 1f);
            w.affinity = StatColor.Brine;
            w.comboResetTime = 0.4f;
            w.description = "A sword in each hand. A relentless four-hit flurry that ends in a spinning slash.";
            w.steps = new[]
            {
                Step("Slash", 8f, 0.04f, 0.06f, 0.10f, 0.3f, new Vector2(0.95f, 0.9f), new Vector2(1.7f, 1.2f), new Vector2(3.5f, 1.5f), 6f, 0.03f, 0.1f, 3f, 0.5f),
                Step("Slash", 8f, 0.04f, 0.06f, 0.10f, 0.3f, new Vector2(0.95f, 0.9f), new Vector2(1.7f, 1.2f), new Vector2(3.5f, 1.5f), 6f, 0.03f, 0.1f, 3f, 0.5f),
                Step("Cross", 10f, 0.05f, 0.07f, 0.12f, 0.3f, new Vector2(1.0f, 0.9f), new Vector2(2.0f, 1.3f), new Vector2(4.5f, 2f), 8f, 0.04f, 0.14f, 3.5f, 0.45f),
                Step("Whirl", 16f, 0.09f, 0.10f, 0.30f, 0.5f, new Vector2(0.4f, 0.9f), new Vector2(3.0f, 1.5f), new Vector2(7f, 4f), 16f, 0.08f, 0.3f, 2f, 0.4f)
            };
            return Clips(w, "dual_a", "dual_b", "dual_cross", "whirl");
        }

        public static WeaponData Greatsword()
        {
            var w = Make<WeaponData>("Greatsword");
            w.displayName = "Greatsword";
            w.tint = new Color(1f, 0.8f, 0.5f);
            w.affinity = StatColor.Ember;
            w.comboResetTime = 0.6f;
            w.description = "Two hands, two slow cleaves. Hits hard and wide, and knocks enemies back.";
            w.steps = new[]
            {
                Step("Cleave", 26f, 0.22f, 0.10f, 0.40f, 0.5f, new Vector2(1.3f, 0.95f), new Vector2(2.8f, 1.7f), new Vector2(9f, 4f), 24f, 0.11f, 0.45f, 2.5f, 0.25f),
                Step("Overhead", 40f, 0.34f, 0.11f, 0.55f, 0.55f, new Vector2(1.4f, 1.0f), new Vector2(3.0f, 2.1f), new Vector2(11f, 7f), 38f, 0.16f, 0.6f, 2f, 0.2f),
                Step("Backhand", 34f, 0.3f, 0.11f, 0.5f, 0.55f, new Vector2(1.3f, 0.95f), new Vector2(3.2f, 1.7f), new Vector2(12f, 5f), 34f, 0.14f, 0.5f, 2.5f, 0.22f)
            };
            return Clips(w, "great_sweep", "great_overhead", "great_slash3");
        }

        public static WeaponData Shield()
        {
            var w = Make<WeaponData>("TowerShield");
            w.displayName = "Tower Shield";
            w.kind = WeaponKind.Shield;
            w.tint = new Color(0.7f, 0.85f, 1f);
            w.affinity = StatColor.Moss;
            w.steps = new AttackStep[0];
            w.description = "Tap to parry, hold to block. Heavy and dependable.";
            w.shield = new ShieldSettings { lowerTime = 0.2f };
            w.blockClip = "block_tower";
            return w;
        }

        public static WeaponData Buckler()
        {
            var w = Make<WeaponData>("Buckler");
            w.displayName = "Buckler";
            w.kind = WeaponKind.Shield;
            w.tint = new Color(0.85f, 0.95f, 1f);
            w.affinity = StatColor.Brine;
            w.steps = new AttackStep[0];
            w.description = "A generous parry window, but it barely blocks.";
            w.blockClip = "block_buckler";
            w.shield = new ShieldSettings
            {
                parryWindow = 0.28f, blockDamageReduction = 0.55f, moveScale = 0.7f, parryStun = 1.0f,
                blockPushback = 2f, parryHitStop = 0.12f, parryShake = 0.5f, lowerTime = 0.15f
            };
            return w;
        }

        // ------------------------------------------------------------------ skills

        static SkillData Skill(string name, string title, SkillKind kind, StatColor affinity, Color tint, float cooldown, float damage,
            float radius, float duration, float fireRate, StatusType status, float statusDuration, string description)
        {
            var s = Make<SkillData>(name);
            s.displayName = title; s.kind = kind; s.affinity = affinity; s.tint = tint; s.cooldown = cooldown; s.damage = damage;
            s.radius = radius; s.duration = duration; s.fireRate = fireRate; s.status = status; s.statusDuration = statusDuration;
            s.description = description;
            return s;
        }

        public static SkillData[] Skills() => new[]
        {
            Skill("PowderKeg", "Powder Keg", SkillKind.Grenade, StatColor.Ember, new Color(1f, 0.5f, 0.2f), 9f, 38f, 2.4f, 0f, 0f, StatusType.Burn, 4f,
                "Lob a keg that bursts and sets enemies alight."),
            Skill("BellTurret", "Bell Turret", SkillKind.Turret, StatColor.Brine, new Color(0.4f, 0.9f, 1f), 16f, 7f, 0f, 9f, 2.5f, StatusType.None, 0f,
                "A tolling turret that shoots the nearest enemy."),
            Skill("SnareTrap", "Snare Trap", SkillKind.Trap, StatColor.Moss, new Color(0.5f, 0.9f, 0.4f), 11f, 14f, 1.8f, 0f, 0f, StatusType.Stun, 2.2f,
                "Springs on the first enemy to step on it, stunning everything nearby."),
            Skill("VenomFlask", "Venom Flask", SkillKind.Grenade, StatColor.Moss, new Color(0.55f, 0.95f, 0.3f), 12f, 18f, 2.8f, 0f, 0f, StatusType.Poison, 6f,
                "A thrown flask that poisons everything it splashes.")
        };

        // ------------------------------------------------------------------ gear

        static GearData Gear(string name, string title, GearSlot slot, Color tint, string description, params StatEntry[] mods)
        {
            var g = Make<GearData>(name);
            g.displayName = title; g.slot = slot; g.tint = tint; g.description = description; g.modifiers = mods;
            return g;
        }

        static StatEntry Flat(StatKind s, float v) => new StatEntry(s, ModOp.Flat, v);
        static StatEntry Pct(StatKind s, float v) => new StatEntry(s, ModOp.PercentAdd, v);

        public static GearData[] Amulets() => new[]
        {
            Gear("FerrymansCoin", "Ferryman's Coin", GearSlot.Amulet, new Color(1f, 0.85f, 0.3f), "The ferryman always takes his cut. So do you.",
                Pct(StatKind.GoldFind, 0.25f), Flat(StatKind.GoldOnKill, 1f)),
            Gear("CrackedReliquary", "Cracked Reliquary", GearSlot.Amulet, new Color(0.7f, 0.4f, 0.9f), "Vitality leaks in through the cracks, and so does harm.",
                Flat(StatKind.MaxHealth, 25f), Pct(StatKind.DamageTaken, 0.10f)),
            Gear("BellFragment", "Bell Fragment", GearSlot.Amulet, new Color(1f, 0.95f, 0.5f), "A shard that rings true on a clean strike.",
                Flat(StatKind.CritChance, 0.12f), Flat(StatKind.CritDamage, 0.15f)),
            Gear("LanternCharm", "Lantern Charm", GearSlot.Amulet, new Color(1f, 0.65f, 0.25f), "Light feet and a quick hand.",
                Pct(StatKind.MoveSpeed, 0.10f), Pct(StatKind.AttackSpeed, 0.05f)),
            Gear("TideLocket", "Tide Locket", GearSlot.Amulet, new Color(0.4f, 0.85f, 0.85f), "Patience, as the tide teaches it.",
                Pct(StatKind.ParryWindow, 0.4f), Pct(StatKind.DamageTaken, -0.05f)),
            Gear("EmberSigil", "Ember Sigil", GearSlot.Amulet, new Color(1f, 0.35f, 0.25f), "Burns hot and wants more.",
                Pct(StatKind.DamageDealt, 0.14f))
        };

        public static GearData[] Outfits() => new[]
        {
            Gear("ApprenticeRobes", "Apprentice Robes", GearSlot.Outfit, new Color(0.95f, 0.9f, 0.8f), "The robes you died in. Still warm.",
                Flat(StatKind.MaxHealth, 10f)),
            Gear("DrownedVestments", "Drowned Vestments", GearSlot.Outfit, new Color(0.35f, 0.65f, 0.6f), "Heavy and sodden, hard to hurt.",
                Pct(StatKind.DamageTaken, -0.12f), Pct(StatKind.MoveSpeed, -0.05f)),
            Gear("BoathookHarness", "Boathook Harness", GearSlot.Outfit, new Color(0.6f, 0.45f, 0.3f), "Leather and rope for the quick of arm.",
                Pct(StatKind.AttackSpeed, 0.15f)),
            Gear("AshCowl", "Ash Cowl", GearSlot.Outfit, new Color(0.45f, 0.45f, 0.5f), "Glass cannon, charcoal edition.",
                Pct(StatKind.DamageDealt, 0.20f), Flat(StatKind.MaxHealth, -15f)),
            Gear("VerdigrisMantle", "Verdigris Mantle", GearSlot.Outfit, new Color(0.4f, 0.8f, 0.55f), "Green with age, feeds on the fallen.",
                Flat(StatKind.HealOnKill, 2f), Flat(StatKind.MaxHealth, 10f))
        };

        // ------------------------------------------------------------------ affixes

        static AffixData Stat(string id, string label, AffixTarget targets, StatKind stat, ModOp op, float min, float max, int weight = 10, bool percent = false, bool curse = false)
        {
            var a = Make<AffixData>(id);
            a.id = id; a.label = label; a.kind = AffixKind.Stat; a.targets = targets; a.stat = stat; a.op = op;
            a.min = min; a.max = max; a.weight = weight; a.showAsPercent = percent; a.isCurse = curse;
            return a;
        }

        static AffixData OnHit(string id, string label, StatusType status, float minChance, float maxChance, float duration, float potency = 1f)
        {
            var a = Make<AffixData>(id);
            a.id = id; a.label = label; a.kind = AffixKind.OnHitStatus; a.targets = AffixTarget.Weapon;
            a.status = status; a.min = minChance; a.max = maxChance; a.statusDuration = duration; a.statusPotency = potency; a.weight = 8;
            return a;
        }

        public static AffixData[] Affixes() => new[]
        {
            // weapon
            Stat("w_damage", "{0} damage", AffixTarget.Weapon, StatKind.DamageDealt, ModOp.PercentAdd, 0.06f, 0.16f, 14),
            Stat("w_crit", "{0} crit chance", AffixTarget.Weapon, StatKind.CritChance, ModOp.Flat, 0.04f, 0.10f, 10, true),
            Stat("w_critdmg", "{0} crit damage", AffixTarget.Weapon, StatKind.CritDamage, ModOp.Flat, 0.15f, 0.40f, 8, true),
            OnHit("w_burn", "{0} chance to burn", StatusType.Burn, 0.15f, 0.30f, 3.5f),
            OnHit("w_poison", "{0} chance to poison", StatusType.Poison, 0.15f, 0.30f, 5f),
            OnHit("w_bleed", "{0} chance to bleed", StatusType.Bleed, 0.15f, 0.30f, 4f),
            OnHit("w_slow", "{0} chance to slow", StatusType.Slow, 0.20f, 0.35f, 3f),
            OnHit("w_stun", "{0} chance to stun", StatusType.Stun, 0.06f, 0.12f, 1.2f),
            // skills
            Stat("s_damage", "{0} skill damage", AffixTarget.Skill, StatKind.DamageDealt, ModOp.PercentAdd, 0.08f, 0.20f, 14),
            Stat("s_cooldown", "{0} cooldown speed", AffixTarget.Skill, StatKind.CooldownRate, ModOp.PercentAdd, 0.10f, 0.25f, 12),
            Stat("s_potent", "{0} potency", AffixTarget.Skill, StatKind.DamageDealt, ModOp.PercentAdd, 0.15f, 0.30f, 6),
            Stat("s_swift", "{0} recharge speed", AffixTarget.Skill, StatKind.CooldownRate, ModOp.PercentAdd, 0.15f, 0.30f, 6),
            // gear
            Stat("g_health", "{0} max health", AffixTarget.Gear, StatKind.MaxHealth, ModOp.Flat, 10f, 30f, 12),
            Stat("g_speed", "{0} move speed", AffixTarget.Gear, StatKind.MoveSpeed, ModOp.PercentAdd, 0.04f, 0.10f, 10),
            Stat("g_attackspeed", "{0} attack speed", AffixTarget.Gear, StatKind.AttackSpeed, ModOp.PercentAdd, 0.04f, 0.10f, 10),
            Stat("g_resist", "{0} damage taken", AffixTarget.Gear, StatKind.DamageTaken, ModOp.PercentAdd, -0.10f, -0.04f, 10),
            Stat("g_gold", "{0} gold find", AffixTarget.Gear, StatKind.GoldFind, ModOp.PercentAdd, 0.10f, 0.30f, 8),
            Stat("g_heal", "{0} health per kill", AffixTarget.Gear, StatKind.HealOnKill, ModOp.Flat, 1f, 3f, 8),
            Stat("g_cooldown", "{0} skill cooldown speed", AffixTarget.Gear, StatKind.CooldownRate, ModOp.PercentAdd, 0.08f, 0.20f, 8),
            Stat("g_damage", "{0} damage", AffixTarget.Gear, StatKind.DamageDealt, ModOp.PercentAdd, 0.04f, 0.10f, 10),
            // curses (only on cursed items)
            Stat("c_frail", "{0} max health", AffixTarget.Gear, StatKind.MaxHealth, ModOp.Flat, -30f, -15f, 10, false, true),
            Stat("c_brittle", "+{0} damage taken", AffixTarget.Gear, StatKind.DamageTaken, ModOp.PercentAdd, 0.12f, 0.25f, 10, false, true),
            Stat("c_sluggish", "{0} move speed", AffixTarget.Gear, StatKind.MoveSpeed, ModOp.PercentAdd, -0.12f, -0.06f, 10, false, true),
            Stat("c_dull", "{0} damage", AffixTarget.Weapon | AffixTarget.Skill, StatKind.DamageDealt, ModOp.PercentAdd, -0.20f, -0.10f, 10, false, true)
        };

        // ------------------------------------------------------------------ mutations

        static MutationData Mutation(string name, string title, string description, Color tint, MutationEffect effect = MutationEffect.None, float value = 0f, params StatEntry[] mods)
        {
            var m = Make<MutationData>(name);
            m.displayName = title; m.description = description; m.tint = tint; m.effect = effect; m.effectValue = value; m.modifiers = mods;
            return m;
        }

        public static MutationData[] Mutations() => new[]
        {
            Mutation("TemperedFrame", "Tempered Frame", "+25% max health.", new Color(0.9f, 0.4f, 0.4f), MutationEffect.None, 0f, Pct(StatKind.MaxHealth, 0.25f)),
            Mutation("FleetFoot", "Fleet Foot", "+12% move speed and +8% attack speed.", new Color(1f, 0.8f, 0.3f), MutationEffect.None, 0f, Pct(StatKind.MoveSpeed, 0.12f), Pct(StatKind.AttackSpeed, 0.08f)),
            Mutation("Recklessness", "Recklessness", "+25% damage, but you take 15% more.", new Color(1f, 0.35f, 0.25f), MutationEffect.None, 0f, Pct(StatKind.DamageDealt, 0.25f), Pct(StatKind.DamageTaken, 0.15f)),
            Mutation("Bloodletter", "Bloodletter", "Heal 3 health on every kill.", new Color(0.9f, 0.2f, 0.3f), MutationEffect.HealOnKill, 3f),
            Mutation("Counterstrike", "Counterstrike", "After a perfect parry, your next swing deals 2.5x damage.", new Color(0.5f, 0.85f, 1f), MutationEffect.ParryRiposte, 2.5f),
            Mutation("Momentum", "Momentum", "Each kill grants +8% damage for 5 seconds (stacks to 5).", new Color(1f, 0.6f, 0.2f), MutationEffect.Momentum, 0.08f),
            Mutation("SecondWind", "Second Wind", "Once per run, a lethal hit leaves you at 1 health, then restores 30%.", new Color(1f, 0.9f, 0.5f), MutationEffect.SecondWind, 0.3f),
            Mutation("Gilded", "Gilded", "+30% gold find.", new Color(1f, 0.85f, 0.3f), MutationEffect.None, 0f, Pct(StatKind.GoldFind, 0.30f))
        };

        // ------------------------------------------------------------------ database

        /// <summary>A complete in-memory database (not saved as assets). Used by tests and as the editor builder's input.</summary>
        public static LootDatabase CreateDatabase()
        {
            var db = Make<LootDatabase>("LootDatabase");
            var sword = Sword();
            var shield = Shield();
            var outfits = Outfits();
            db.weapons = new[] { Dagger(), sword, Hammer(), Boathook(), shield, Buckler(), Bow(), TwinBlades(), Greatsword() };
            db.skills = Skills();
            db.amulets = Amulets();
            db.outfits = outfits;
            db.affixes = Affixes();
            db.mutations = Mutations();
            db.startingWeapon1 = sword;
            db.startingWeapon2 = shield;
            db.startingOutfit = outfits[0];
            return db;
        }
    }
}
