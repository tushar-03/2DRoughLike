# PROJECT BRIEF (v2): 2D Action-Platformer Roguelite
# Gameplay: Dead Cells | Art Direction: Hades | Engine: Unity 6 via MCP

## 0. YOUR ROLE
You are the lead game developer, technical artist, and game designer. You work inside Unity 6 through the Unity MCP connection (official Unity CLI / Pipeline package). You build everything from scratch (code, scenes, prefabs, shaders, VFX, UI, data, art), work in small verified steps, and ask me when a decision would meaningfully change the game.

## 1. CURRENT PROJECT STATE (verified by reading files)
- Project folder: `C:\unityProjects\Roughlike` (git repo exists, Unity .gitignore and .gitattributes present)
- Unity **6000.5.6f1**, **URP 17.6.0 with 2D Renderer** (Renderer2D.asset present), Lit2D scene template, SampleScene only. No scripts or custom folders yet.
- Packages present: Input System 1.20.1, Cinemachine 3.1.7, 2D Animation 15.1.0, Addressables 2.9.1, Test Framework 1.7.0, uGUI 2.5.0, 2D Tilemap + Extras, SpriteShape, Aseprite/PSD importers, Timeline, Shader Graph (via URP).
- MCP: official Unity CLI + `com.unity.pipeline` 0.8.0-exp.1 installed, `unity status` = ready, Claude Desktop configured via `unity mcp configure claude`.
- **Also present:** `com.coplaydev.unity-mcp` (community MCP). It may conflict with the official one. **Ask me before removing it** from `Packages/manifest.json`.
- Unneeded packages to propose removing (ask first): visualscripting, multiplayer.center, ai.assistant, and the Terrain/XR/Vehicles/Wind/Cloth/Umbra modules.

## 2. DECISIONS ALREADY MADE (do not re-ask)
- **Combat mix:** melee-led, with **shields and timed parry as a core pillar**. Ranged and magic weapons are a smaller share.
- **Movement:** base kit has NO double jump, wall-jump, or air dash. These come only from traversal runes. Ask me later whether the roll works in mid-air.
- **Story:** yes, a Hades-style story and dialogue layer. Setting: **The Drowned Reliquary**. You are a bell-ringer's apprentice, killed while sealing a sunken cathedral-city, and each death returns you to a lantern-lit boathouse hub. Tone: melancholy but warm, with dry humor from a ferryman and an archivist. Original characters only.
- **Players:** singleplayer only. Do not build network or co-op architecture.
- **UI:** uGUI, with full controller navigation.
- **Audio:** Unity built-in audio with mixer groups (Master, Music, SFX, UI, Ambient), synthesized placeholder sounds.
- **Art:** **you (Claude) make the art.** See section 12 for the honest limits and the swap-in pipeline.

## 3. STILL OPEN (ask me in one small batch after M0 is set up)
1. Extra art references (games or screenshots) beyond Hades and Dead Cells
2. Minimum PC specs and target resolution
3. Content or themes to avoid (gore level, religious imagery, and so on)
4. Amulet/outfit slots: yes or no
5. Whether the roll works in mid-air

## 4. PITCH
A fast, fluid, side-scrolling 2D action-platformer roguelite. Gameplay follows Dead Cells: tight platforming, a roll with i-frames, two weapon slots plus two skill slots, procedurally assembled interconnected levels, permadeath, and permanent unlocks. Visuals follow Hades: bold hand-painted-style illustration, thick expressive outlines, saturated mythic color palettes, glowing rim light, and ornate UI. Not pixel art.

## 5. DESIGN PILLARS
1. **Movement and combat feel first.** Snappy, responsive, no stamina. Every hit has hitstop, screenshake, VFX, and sound hooks.
2. **Speed and flow.** Rewarding fast, aggressive play. Death resets the run, but unlocks carry over.
3. **Build variety through loot.** Weapons, skills, and mutations create different builds each run.
4. **Hades-style visual identity.** Strong silhouettes, bold color, painterly richness, readable at speed.
5. **Readability.** Telegraphs, hazards, and pickups stand out against beautiful backgrounds.

## 6. TECH STACK
- Unity 6000.5.6f1, **URP 2D Renderer**
- **Input System** (gamepad + keyboard, rebindable), **Cinemachine 3**, **2D Animation / Sprite Skinning** plus frame-by-frame VFX
- 2D Lights, **Shader Graph**, Tilemaps, Volume post-processing, **Addressables**, **ScriptableObjects** for all data, **Unity Test Framework**
- C# with assembly definitions and namespaces. Git, commit at each milestone.
- Target: PC (Windows), 60 FPS minimum, gamepad first-class, Steam Deck friendly

## 7. MCP WORKFLOW RULES
- Small vertical slices. After every change: let it compile, **read the console**, fix errors and warnings, and test in Play Mode when relevant.
- Never leave the project broken. If something fails twice, stop, explain, and offer options.
- Ask before destructive or large actions (deleting assets, restructuring folders, changing render settings, editing manifest.json, overwriting files).
- Ask before any git action that changes state (commit, push, pull, checkout, reset). Read-only git is fine.
- Maintain `DESIGN.md`, `PROGRESS.md`, `ART_TODO.md` in the repo root, updated after each milestone.
- Modular, commented code. Composition and events over inheritance. No god classes. Minimal singletons.
- Every tuning value (jump height, roll distance, i-frames, damage, cooldowns, hitstop) is exposed in the Inspector or a ScriptableObject.
- After each milestone, give a short summary and a "how to test" checklist.

## 8. PLAYER MOVEMENT (Dead Cells-style)
- Custom **kinematic character controller** (explain the choice over Rigidbody)
- Run with tuned acceleration/deceleration, **variable-height jump**, **coyote time, jump buffering, input buffering**, fast-fall
- **Roll** with invulnerable frames, fixed duration, passes through enemies, usable on the ground. It cancels most attack recoveries.
- **Ladders and ledge-grab/climb**, one-way platforms, slopes, moving platforms
- **Ground pound / downward strike** (weapon-dependent)
- **Traversal runes** from meta-progression: wall-climb, vine/grapple, teleport, ground-slam through floors, and also double jump, wall-jump and air dash

## 9. COMBAT
- **Loadout:** 2 weapon slots with instant swap, 2 skill slots (grenades, turrets, traps; on cooldown), 1 health flask (refills between biomes)
- **Weapon categories:** fast melee (daggers), balanced (swords), heavy (hammers/axes), whips/reach, **shields (parry and block, core)**, bows/crossbows, throwables, magic/wands
- **Parry and block:** timed parry windows, perfect-parry reward
- Combo chains with input buffering, per-weapon timings, hit frames and hitbox shapes, all data-driven
- **Scroll stat system:** three original stats that scale weapon and skill damage by color, plus health. Items have color affinities. Scrolls drop between rooms.
- **Mutations:** passive perks at set points per run, limited slots
- Status effects: Burn, Poison, Bleed, Freeze/Slow, Stun, Shock, Oil-and-fire interaction, Weaken
- Enemy reactions: stagger, knockback, juggle, armor/shield break, elites with modifiers
- **Affixes and rarity** on gear, tiered upgrades, random modifiers, cursed items (risk-reward)
- Game feel: hitstop, screenshake, hit flash, damage numbers (toggleable), slash trails, crit
- Telegraphs on every enemy attack with a consistent visual language

## 10. ROGUELIKE RULES
- **Permadeath:** dying ends the run. You keep meta-unlocks and lose weapons, gold, and unspent progress.
- **Procedural levels:** each biome is assembled from hand-authored room chunks, with a start, an exit, and branching alternate paths. Seeded RNG with shareable seeds.
- **Branching path:** after each biome, pick between 2 next biomes
- **No checkpoints mid-biome.** Flask and shops are the safety net.
- **Currencies:** Gold (spent in the run), **Cells** (banked at the biome exit door to unlock things permanently, lost on death if not banked), and blueprint drops
- **Blueprints and unlocks** add new weapons and skills to the loot pool
- Special rooms: shops, challenge/timed doors, curse chests, secret rooms, elite rooms, rune/gate rooms, healing spots
- **Boss Cell difficulty tiers:** stackable modifiers after the final boss (name them originally)
- Difficulty ramps by biome, with fair deaths always

## 11. META-PROGRESSION, HUB AND STORY
- A small explorable **hub: a lantern-lit boathouse** (Drowned Reliquary). Spend Cells and unlock gear there.
- **NPC shop** for permanent unlocks, an unlock-into-loot-pool system, and starter loadout options
- **Story layer:** NPC conversations that advance on each death (ferryman, archivist, more to propose). Dialogue is data-driven and localization-ready.
- Optional: run history, stats, bestiary/codex

## 12. ENEMIES, BOSSES, HAZARDS
- Archetypes: melee grunt, ranged shooter, flyer, shield-bearer, charger, summoner, exploder, sniper, elite variants, undead/respawning types
- AI via a clean state machine: patrol, aggro, attack with telegraph, recover, retreat
- **3 biomes to start, each with 1 boss and 1 mini-boss/elite.** Multi-phase bosses with scripted intros, health bars and telegraphed patterns.
- Hazards: spikes, falling rocks, poison pools, moving platforms, traps, breakable walls

## 13. BIOMES (propose better ones that fit the Drowned Reliquary, after asking me)
1. **Ember Catacombs:** deep teal and crimson shadows, glowing runes, bone and brass
2. **Gilded Marsh / Sunken Halls:** warm gold, emerald and amber light, reflective water
3. **Ashen Spires:** orange lava glow against charcoal, drifting embers, heat haze
Each biome has its own palette, ambient particles, hazards, enemy roster, music hooks, and boss.

## 14. ART DIRECTION: HADES-STYLE (side-view, not pixel art)
### Look
- Painterly illustration with **bold, thick, variable-width dark outlines** on characters, enemies and key props
- **Saturated, high-contrast palettes** per biome (dominant hue plus contrasting accent)
- **Cel-shaded characters**: 2-3 tone steps, sharp **rim light**, glowing emissive accents
- Stylized, slightly exaggerated proportions with strong silhouettes
- **Backgrounds:** layered parallax with atmospheric perspective, softer and slightly desaturated so characters pop. Dark framing foreground and vignette.
- **VFX:** bold, shape-driven, flipbook-style slashes, hit bursts, dissolve, glowing trails, embers. Nothing realistic.
- **UI:** ornate, mythic frames, bold typography, glowing rarity colors
- **No pixel art, no point filtering.** High-res sprites with bilinear filtering.

### Rendering (URP 2D)
- 2D Lights, per-biome color grading, bloom, vignette, subtle grain, optional chromatic aberration on hits
- **Shader Graph shaders:** outline (inner and outer), rim light, cel/toon ramp, hit flash, dissolve, wind sway, water/lava, glow pulse, parallax fog, palette swap
- Normal-mapped sprites where feasible

### Asset creation: Claude makes the art, honest limits
You cannot hand-paint Hades-quality art. So:
1. Create the art yourself with code and shaders: **procedural vector-style sprites** (generated SVG or textures), gradients and noise, outline and rim shaders, painterly parallax layers, flipbook VFX. The goal is a cohesive, stylized look that is playable and consistent. Be upfront that it will look like a stylized vector game, not hand-painted.
2. Build a **swap-in pipeline:** every character, enemy, prop, tile, background layer and VFX sits behind a prefab, Sprite Library or ScriptableObject slot, so better art (AI-generated, packs, commissioned) drops in with no code changes.
3. Procedural animation where possible (squash and stretch, sway, anticipation, breathing, bone rigs, tweens) and a reusable rig template.
4. Maintain **ART_TODO.md** listing each placeholder with dimensions, style notes and priority.
5. Produce a **style guide** (palette, outline thickness, shading rules, resolution) so later art matches.

## 15. AUDIO
- Audio manager with mixer groups, adaptive music layers (explore, combat, boss), positional SFX
- Every gameplay event fires an audio hook, with simple synthesized placeholder sounds
- Unity built-in audio (no FMOD or Wwise)

## 16. UI/UX (uGUI)
- HUD: health, flask charges, both weapon slots, both skill cooldowns, scroll stats, gold, cells, mutation icons, boss health bar, minimap
- Menus: main, pause, settings (video, audio, rebinding, accessibility), run summary on death, hub menus, dialogue UI
- Full controller navigation, localization-ready (no hardcoded strings)
- **Accessibility:** colorblind-safe telegraphs, screenshake and flash sliders, hold/toggle options, assist mode, subtitles

## 17. ARCHITECTURE
- Folders: `Art/`, `Audio/`, `Prefabs/`, `Scenes/`, `Scripts/` (Core, Player, Combat, Enemies, Items, Rooms, Meta, UI, Audio, Utils), `Data/`, `Shaders/`, `Tests/`
- Modules: event bus, object pooling, stat/modifier system, health/damage system, status effects, run/level generator, seeded RNG, save/load (versioned JSON with backup), game state machine (Boot, Menu, Hub, Run, Death, Victory), settings service
- Items (weapons, skills, mutations) are **data-driven ScriptableObjects**
- Pool everything spawned often, keep allocations out of hot paths, verify with the Profiler

## 18. DEBUG AND DEV TOOLS (build early)
- In-game debug console: give item, spawn enemy, set seed, teleport to room, god mode, kill all, show hitboxes, time scale, skip to boss, add cells/gold
- **Room-chunk authoring tool** with connection points, spawn points, doors, loot slots
- Gizmos for hitboxes, AI ranges, camera bounds, chunk connectors
- Level-generator visualizer showing the chunk graph for a seed

## 19. MILESTONES (stop for my review after each)
1. **M0, Setup:** verify MCP, read console, folders, assembly definitions, docs, optional package cleanup (ask first)
2. **M1, Movement prototype:** gray-box level, full movement, roll, ladders, ledges, camera
3. **M2, Combat prototype:** weapon swap, shield and parry, first 3 weapon types, hitboxes, hitstop, training dummy, 2 enemy types with telegraphs
4. **M3, Level generation:** chunk-based generator, seeded RNG, exits, doors, death and restart
5. **M4, Loot and stats:** scroll stats, rarity and affixes, skills, mutations, shops, gold
6. **M5, Hades-style visual slice:** biome 1 with the full look
7. **M6, Content expansion:** more weapons, skills, enemies, special rooms, challenge doors
8. **M7, Boss and biome 1 complete,** end to end
9. **M8, Meta-progression:** hub, cell banking, blueprints, unlock tree, runes, save/load, story dialogue
10. **M9, Biomes 2 and 3,** bosses, branching paths, difficulty tiers
11. **M10, Polish:** audio, UI, accessibility, performance, build pipeline, playtest pass

## 20. DEFINITION OF DONE (per feature)
- Compiles with zero errors and no new warnings
- Tested in Play Mode with a "how to test" note
- Values tweakable in the Inspector or data assets
- Documented in PROGRESS.md and committed to Git (ask before committing)
- Regression tests still pass

## 21. START NOW
Begin M0. First confirm the Unity MCP tools are available, read the console, and report the Unity version, packages and pipeline. Then ask me the open questions in section 3 (one small batch). Do not start M1 until I approve M0.
