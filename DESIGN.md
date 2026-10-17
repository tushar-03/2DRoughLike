# DESIGN — The Drowned Reliquary

2D action-platformer roguelite. Gameplay: Dead Cells. Art direction: Hades (side-view, painterly vector look, not pixel art).
Engine: Unity 6000.5.6f1, URP 2D Renderer. Singleplayer only. PC (Windows), 60 FPS min, gamepad first-class, Steam Deck friendly.

## Pillars
1. Movement and combat feel first (hitstop, shake, VFX, audio hooks on every hit).
2. Speed and flow; death resets the run, unlocks carry over.
3. Build variety through loot (weapons, skills, mutations).
4. Hades-style visual identity; strong silhouettes, bold color.
5. Readability: telegraphs, hazards and pickups stand out.

## Decisions (locked)
- Combat: melee-led; shields and timed parry are a core pillar; ranged/magic a smaller share.
- Movement: base kit has NO double jump, wall-jump or air dash (traversal runes only). Roll works mid-air (decided; jump-and-roll is a core movement tech).
- Equipment: amulet/outfit slots are IN. Pickup items (amulets, outfits) are data-driven ScriptableObjects that drop in runs and equip into slots, granting stat modifiers / passive effects through the stat-modifier system (scheduled for M4; slot count and rules to be proposed then).
- Story: Hades-style dialogue layer. Setting: The Drowned Reliquary. Player is a bell-ringer's apprentice who died sealing a sunken cathedral-city; each death returns to a lantern-lit boathouse hub. Melancholy but warm; dry humor from a ferryman and an archivist. Original characters only.
- UI: uGUI, full controller navigation, localization-ready.
- Audio: Unity built-in audio, mixer groups Master/Music/SFX/UI/Ambient, synthesized placeholders.
- Art: made by Claude (procedural vector-style + shaders), behind a swap-in pipeline (see ART_TODO.md).

## Architecture
- Folders under `Assets/`: Art, Audio, Prefabs, Data, Shaders, Scenes, Scripts/{Core,Player,Combat,Enemies,Items,Rooms,Meta,UI,Audio,Utils}, Tests/{EditMode,PlayMode}.
- One asmdef per Scripts folder, namespace `Roughlike.<Module>`. References are added deliberately when a milestone needs them.
- Allowed dependencies: Utils <- Core (incl. the generic KinematicMotor2D, EventBus, layers) <- Player <- Combat <- Enemies <- Rooms. Modules talk through `EventBus<T>` events (hits, swings, parries, enemy deaths); presentation systems subscribe, gameplay never calls them.
- Weapon slots: each slot has its own button (Dead Cells model), which is the instant swap. Shields live in a slot like any weapon: tap = parry window, hold = block.
- Composition and events over inheritance, no god classes, minimal singletons.
- All tuning values in the Inspector or ScriptableObjects; all items/weapons/skills/mutations are ScriptableObjects.
- Planned core modules: event bus, object pooling, stat/modifier system, health/damage, status effects, seeded RNG, level generator, versioned JSON save with backup, game state machine (Boot, Menu, Hub, Run, Death, Victory), settings service.

## Level generation (M3)
- Grid of 30x18 rooms. A seeded generator makes a main path (Start to Exit, mostly rightward with vertical steps) plus dead-end Treasure branches. Rooms are built from their openings so connections always line up; every connection is traversable with the base kit (jump 3.2, doors 6 high, stairs of one-way platforms in shafts).
- Determinism rule: all procedural code uses `SeededRng`; sub-systems use `Fork(salt)` so changing one never shifts another. Seeds are shareable codes (`SeedCode`).
- Rooms are procedural graybox now; hand-authored chunk prefabs plug in through `RoomChunk` markers.

## Loot and stats (M4)
- Stats are a `StatSheet` of modifiers (flat, additive %, multiplicative %). Scroll colours are Ember (red), Brine (cyan), Moss (green); weapons and skills carry colour affinities and scale +12% per scroll level in those colours.
- Items are instances of data assets with a rolled rarity (Common to Legendary), tier (1-4) and affixes; weapon and skill affixes are local to the item, gear affixes are global. Cursed items trade a bad affix for extra power.
- Combat reaches the item system only through `IPlayerModifiers` (per-weapon damage, crit, status procs, speed). Items, shops, altars and pickups talk to the HUD through events (`ToastEvent`, `InteractPromptEvent`, `OpenShopEvent`, `ReplaceItemRequest`, ...); the UI module is the only one that knows about menus.
- Dependency chain is now Utils <- Core <- Player <- Combat <- Enemies <- Items <- Rooms <- UI.
- Rule for ScriptableObjects and MonoBehaviours that go into assets or prefabs: one class per file, file named after the class (Unity needs it).

## Enemies (M5)
- `EnemyController` owns what every enemy shares; an `EnemyBehavior` strategy owns what makes an archetype different (movement, when to attack, telegraph shape, the attack). Adding an archetype means one behaviour class plus an `EnemyDefinition` field set; adding an enemy of an existing archetype is only a new asset.
- Every attack goes through a telegraph (box, rotated line or circle) that fills exactly as the hit lands. Behaviours can keep adjusting a telegraph while it runs (a laser tracking the player) and lock it before the hit.
- Behaviours see hits first through the `IHitFilter` chain (shields reduce, redirect or punish), then the stun bonus applies.

## Art pipeline (M5)
- All art is generated by code and sits behind swap-in assets: `CharacterArt` (a sheet with one row per clip), `EnvironmentArt` (tiles, backdrop layers, lighting mood, post-processing, props). Runtime code reads only those assets, so authored art replaces generated art with no code change; with no art assigned everything falls back to the graybox look.
- Generators live in `Assets/Scripts/Editor/Art`: a small software rasteriser (`Painter`) plus character, creature, prop and environment painters, driven from `CharacterLibrary`. Output is deterministic.
- Presentation reads gameplay, never the reverse: `SpriteAnimator` plays clips, `PlayerVisual` and `PlayerCombatAnimator` pick player clips, `EnemyAnimDriver` picks enemy clips from AI state. Art must never change solid geometry (a test enforces it).
- Lighting is URP 2D: a global ambient light, point lights for the player and torches, and a global Volume built at runtime from the biome's art.

## Menus and saves (M4.5)
- `SaveService` is the only code that touches files: atomic writes, backup fallback, tests redirect it to a temp folder. Settings and the run save are separate versioned JSON files.
- A run save does not serialise the level. It stores the seed and the changes the player made, and rebuilds the level deterministically before applying them.
- One shared `GameInput` action map is used by gameplay and the rebinding screen; rebinding overrides are stored as (action, group, path) so they survive Input System ID changes.

## Level traversability rule
Every generated level must be finishable with the base kit, and the rule is enforced by code, not by hand-checking rooms. `TraversalValidator` is the oracle (tests run it over many seeds): templates may only add geometry the validator accepts. Mandatory paths use jumping and walking only. Rolls are for combat and optional shortcuts.

## Input, haptics and platforms
- One shared gameplay action map. Keyboard, gamepad and touch all feed it; touch uses on-screen controls bound to a virtual gamepad, so there is no second code path.
- `InputDevices` tracks which device is in use and names controls for it; all prompts read from it.
- `Haptics` is the only code that touches rumble or vibration; gameplay just raises combat events.
- Web: browser-only needs live in `WebBridge` and one `.jslib`; everything else is plain Unity. Saves go through `SaveService`, which flushes to IndexedDB after each write.

## Milestones
M0 Setup · M1 Movement · M2 Combat · M3 Level gen · M4 Loot/stats · M4.5 Menus/saves · M5 Visual slice + enemies · M6 Content · M7 Boss/biome 1 · M8 Meta/hub/story · M9 Biomes 2-3 · M10 Polish.
Review stop after each.

## Open questions
See PROGRESS.md.
