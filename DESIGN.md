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
- One asmdef per Scripts folder, namespace `Roughlike.<Module>`. Dependency direction: Utils <- Core <- everything else. Feature modules do not reference each other directly; they talk via the Core event bus and shared interfaces in Core. (Add references deliberately when a milestone needs them.)
- Allowed dependencies: Utils <- Core (incl. the generic KinematicMotor2D, EventBus, layers) <- Player <- Combat <- Enemies. Modules talk through `EventBus<T>` events (hits, swings, parries, enemy deaths); presentation systems subscribe, gameplay never calls them.
- Weapon slots: each slot has its own button (Dead Cells model), which is the instant swap. Shields live in a slot like any weapon: tap = parry window, hold = block.
- Composition and events over inheritance, no god classes, minimal singletons.
- All tuning values in the Inspector or ScriptableObjects; all items/weapons/skills/mutations are ScriptableObjects.
- Planned core modules: event bus, object pooling, stat/modifier system, health/damage, status effects, seeded RNG, level generator, versioned JSON save with backup, game state machine (Boot, Menu, Hub, Run, Death, Victory), settings service.

## Milestones
M0 Setup · M1 Movement · M2 Combat · M3 Level gen · M4 Loot/stats · M5 Visual slice · M6 Content · M7 Boss/biome 1 · M8 Meta/hub/story · M9 Biomes 2-3 · M10 Polish.
Review stop after each.

## Open questions
See PROGRESS.md.
